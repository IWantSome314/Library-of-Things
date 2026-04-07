using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using StarterApp.Api;
using StarterApp.Database.Data;
using StarterApp.Database.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddScoped<ITokenService, TokenService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 characters long.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await ApplyMigrationsWithRecoveryAsync(db);
    await EnsureDefaultRolesAsync(db);
    await EnsureMockDataAsync(db);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/auth/token", async (
    AppDbContext db,
    ITokenService tokenService,
    LoginRequest request) =>
{
    if (!MiniValidator.TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var user = await db.Users
        .Include(u => u.UserRoles)
        .ThenInclude(ur => ur.Role)
        .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

    if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var roles = user.UserRoles
        .Where(ur => ur.IsActive)
        .Select(ur => ur.Role.Name)
        .ToList();

    var tokenPair = tokenService.CreateTokenPair(user, roles);
    await SaveRefreshTokenAsync(db, tokenService, user.Id, tokenPair.RefreshToken, jwtOptions.RefreshTokenDays);

    return Results.Ok(tokenPair);
});

app.MapPost("/auth/register", async (AppDbContext db, RegisterRequest request) =>
{
    if (!MiniValidator.TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var normalizedEmail = request.Email.Trim().ToLowerInvariant();
    var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
    if (existing is not null)
    {
        return Results.Conflict(new { message = "User with this email already exists." });
    }

    var salt = BCrypt.Net.BCrypt.GenerateSalt();
    var hash = BCrypt.Net.BCrypt.HashPassword(request.Password, salt);

    var user = new User
    {
        FirstName = request.FirstName.Trim(),
        LastName = request.LastName.Trim(),
        Email = normalizedEmail,
        PasswordSalt = salt,
        PasswordHash = hash,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsActive = true
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    var defaultRole = await db.Roles.FirstOrDefaultAsync(r => r.IsDefault);
    if (defaultRole is not null)
    {
        db.UserRoles.Add(new UserRole(user.Id, defaultRole.Id));
        await db.SaveChangesAsync();
    }

    return Results.Ok(new { message = "Registration successful." });
});

app.MapPost("/auth/refresh", async (
    AppDbContext db,
    ITokenService tokenService,
    RefreshRequest request) =>
{
    if (!MiniValidator.TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var refreshHash = tokenService.HashRefreshToken(request.RefreshToken);
    var storedToken = await db.RefreshTokens
        .FirstOrDefaultAsync(rt => rt.TokenHash == refreshHash);

    if (storedToken is null || storedToken.IsRevoked || storedToken.ExpiresAtUtc <= DateTime.UtcNow)
    {
        return Results.Unauthorized();
    }

    var user = await db.Users
        .Include(u => u.UserRoles)
        .ThenInclude(ur => ur.Role)
        .FirstOrDefaultAsync(u => u.Id == storedToken.UserId && u.IsActive);

    if (user is null)
    {
        return Results.Unauthorized();
    }

    storedToken.IsRevoked = true;
    storedToken.RevokedAtUtc = DateTime.UtcNow;

    var roles = user.UserRoles
        .Where(ur => ur.IsActive)
        .Select(ur => ur.Role.Name)
        .ToList();

    var tokenPair = tokenService.CreateTokenPair(user, roles);
    await SaveRefreshTokenAsync(db, tokenService, user.Id, tokenPair.RefreshToken, jwtOptions.RefreshTokenDays);

    await db.SaveChangesAsync();

    return Results.Ok(tokenPair);
});

app.MapGet("/auth/me", async (ClaimsPrincipal principal, AppDbContext db) =>
{
    var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue(ClaimTypes.Name)
        ?? principal.FindFirstValue("sub");

    if (!int.TryParse(userIdClaim, out var userId))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users
        .Include(u => u.UserRoles)
        .ThenInclude(ur => ur.Role)
        .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

    if (user is null)
    {
        return Results.Unauthorized();
    }

    var response = new MeResponse
    {
        UserId = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        Roles = user.UserRoles.Where(ur => ur.IsActive).Select(ur => ur.Role.Name).ToList()
    };

    return Results.Ok(response);
}).RequireAuthorization();

app.MapPost("/auth/change-password", async (
    ClaimsPrincipal principal,
    AppDbContext db,
    ChangePasswordRequest request) =>
{
    if (!MiniValidator.TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue(ClaimTypes.Name)
        ?? principal.FindFirstValue("sub");

    if (!int.TryParse(userIdClaim, out var userId))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
    {
        return Results.BadRequest(new { message = "Current password is incorrect." });
    }

    var salt = BCrypt.Net.BCrypt.GenerateSalt();
    var hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, salt);

    user.PasswordSalt = salt;
    user.PasswordHash = hash;
    user.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Password updated." });
}).RequireAuthorization();

app.MapGet("/items", async (AppDbContext db) =>
{
    var items = await db.Items
        .Include(i => i.OwnerUser)
        .Where(i => i.IsActive)
        .OrderByDescending(i => i.CreatedAtUtc)
        .Select(i => new ItemSummaryResponse
        {
            Id = i.Id,
            Title = i.Title,
            Category = i.Category,
            DailyRate = i.DailyRate,
            Location = i.Location,
            Latitude = i.Latitude,
            Longitude = i.Longitude,
            OwnerUserId = i.OwnerUserId,
            OwnerName = i.OwnerUser.FullName
        })
        .ToListAsync();

    return Results.Ok(items);
});

app.MapGet("/items/{id:int}", async (int id, AppDbContext db) =>
{
    var item = await db.Items
        .Include(i => i.OwnerUser)
        .Where(i => i.Id == id && i.IsActive)
        .Select(i => new ItemDetailResponse
        {
            Id = i.Id,
            Title = i.Title,
            Description = i.Description,
            Category = i.Category,
            DailyRate = i.DailyRate,
            Location = i.Location,
            Latitude = i.Latitude,
            Longitude = i.Longitude,
            OwnerUserId = i.OwnerUserId,
            OwnerName = i.OwnerUser.FullName,
            CreatedAtUtc = i.CreatedAtUtc,
            UpdatedAtUtc = i.UpdatedAtUtc
        })
        .FirstOrDefaultAsync();

    return item is null ? Results.NotFound() : Results.Ok(item);
});

app.MapPost("/items", async (ClaimsPrincipal principal, AppDbContext db, UpsertItemRequest request) =>
{
    if (!MiniValidator.TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var userId = GetUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var item = new Item
    {
        Title = request.Title.Trim(),
        Description = request.Description.Trim(),
        DailyRate = request.DailyRate,
        Category = request.Category.Trim(),
        Location = request.Location.Trim(),
        Latitude = request.Latitude,
        Longitude = request.Longitude,
        OwnerUserId = userId.Value,
        IsActive = true,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    db.Items.Add(item);
    await db.SaveChangesAsync();

    return Results.Created($"/items/{item.Id}", new { item.Id });
}).RequireAuthorization();

app.MapPut("/items/{id:int}", async (int id, ClaimsPrincipal principal, AppDbContext db, UpsertItemRequest request) =>
{
    if (!MiniValidator.TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var userId = GetUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id && i.IsActive);
    if (item is null)
    {
        return Results.NotFound();
    }

    if (item.OwnerUserId != userId.Value)
    {
        return Results.Forbid();
    }

    item.Title = request.Title.Trim();
    item.Description = request.Description.Trim();
    item.DailyRate = request.DailyRate;
    item.Category = request.Category.Trim();
    item.Location = request.Location.Trim();
    item.Latitude = request.Latitude;
    item.Longitude = request.Longitude;
    item.UpdatedAtUtc = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Item updated." });
}).RequireAuthorization();

app.Run();

static int? GetUserId(ClaimsPrincipal principal)
{
    var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue(ClaimTypes.Name)
        ?? principal.FindFirstValue("sub");

    return int.TryParse(userIdClaim, out var userId) ? userId : null;
}

static async Task SaveRefreshTokenAsync(
    AppDbContext db,
    ITokenService tokenService,
    int userId,
    string rawRefreshToken,
    int refreshTokenDays)
{
    var refresh = new RefreshToken
    {
        UserId = userId,
        TokenHash = tokenService.HashRefreshToken(rawRefreshToken),
        ExpiresAtUtc = DateTime.UtcNow.AddDays(refreshTokenDays),
        CreatedAtUtc = DateTime.UtcNow,
        IsRevoked = false
    };

    db.RefreshTokens.Add(refresh);
    await db.SaveChangesAsync();
}

static async Task EnsureDefaultRolesAsync(AppDbContext db)
{
    if (await db.Roles.AnyAsync())
    {
        return;
    }

    var roles = new[]
    {
        new Role { Name = RoleConstants.Admin, Description = "System administrator", IsDefault = false },
        new Role { Name = RoleConstants.OrdinaryUser, Description = "Standard user", IsDefault = true },
        new Role { Name = RoleConstants.SpecialUser, Description = "Special user", IsDefault = false }
    };

    db.Roles.AddRange(roles);
    await db.SaveChangesAsync();
}

static async Task ApplyMigrationsWithRecoveryAsync(AppDbContext db)
{
    const int maxAttempts = 10;
    var delay = TimeSpan.FromSeconds(2);

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            return;
        }
        catch (NpgsqlException) when (attempt < maxAttempts)
        {
            await Task.Delay(delay);
            continue;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P07")
        {
            // Legacy bootstrap recovery: schema exists but migrations history is missing.
            await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
    ""MigrationId"" character varying(150) NOT NULL,
    ""ProductVersion"" character varying(32) NOT NULL,
    CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
);");

            await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
VALUES ('20260210141124_InitialCreate', '9.0.6')
ON CONFLICT (""MigrationId"") DO NOTHING;");

            await db.Database.MigrateAsync();
            return;
        }
    }

    throw new InvalidOperationException("Database did not become ready in time for migrations.");
}

static async Task EnsureMockDataAsync(AppDbContext db)
{
    if (await db.Items.AnyAsync())
    {
        return;
    }

    var defaultUser = await db.Users.FirstOrDefaultAsync();
    if (defaultUser == null)
    {
        var salt = BCrypt.Net.BCrypt.GenerateSalt();
        defaultUser = new User
        {
            FirstName = "Alice",
            LastName = "Mock",
            Email = "mock@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!", salt),
            PasswordSalt = salt,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(defaultUser);
        await db.SaveChangesAsync();
    }

    var items = new[]
    {
        new Item
        {
            Title = "Makita Power Drill",
            Description = "Professional 18V power drill with extra batteries.",
            Category = "Tools",
            Location = "London",
            DailyRate = 12.50m,
            OwnerUserId = defaultUser.Id,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        },
        new Item
        {
            Title = "Mountain Bike",
            Description = "Men's mountain bike, fully serviced.",
            Category = "Sports",
            Location = "Manchester",
            DailyRate = 25.00m,
            OwnerUserId = defaultUser.Id,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        },
        new Item
        {
            Title = "Camping Tent",
            Description = "4-person dome tent, waterproof.",
            Category = "Outdoors",
            Location = "Birmingham",
            DailyRate = 15.00m,
            OwnerUserId = defaultUser.Id,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        },
        new Item
        {
            Title = "DJI Drone",
            Description = "DJI Mini 3 Pro with 4K camera.",
            Category = "Electronics",
            Location = "Edinburgh",
            DailyRate = 45.00m,
            OwnerUserId = defaultUser.Id,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        }
    };

    db.Items.AddRange(items);
    await db.SaveChangesAsync();
}

static class MiniValidator
{
    public static bool TryValidate<T>(T model, out Dictionary<string, string[]> errors)
    {
        var context = new ValidationContext(model!);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model!, context, results, validateAllProperties: true);

        errors = results
            .SelectMany(result => result.MemberNames.Select(member => new { member, result.ErrorMessage }))
            .GroupBy(x => x.member, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.ErrorMessage ?? "Validation error.").ToArray(),
                StringComparer.Ordinal);

        return isValid;
    }
}
