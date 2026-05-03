using System.Reflection;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StarterApp.Database.Models;

namespace StarterApp.Database.Data;

// File purpose:
// EF Core DbContext for the shared data layer.
// Core responsibilities: resolve a reachable Postgres connection string and define entity mappings.
public class AppDbContext : DbContext
{

    public AppDbContext()
    { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;

        // Fallback strategy allows the same binary to run in host, container, and Android emulator contexts.
        var connectionString = ResolveConnectionString();
        optionsBuilder.UseNpgsql(connectionString);
    }

    private static string ResolveConnectionString()
    {
        // Try environment-specified connection first, then known host permutations.
        var envConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(envConnectionString) && CanReachPostgres(envConnectionString))
        {
            return envConnectionString;
        }

        var configuredConnectionString = GetConfiguredConnectionString();
        var defaultConnectionString = configuredConnectionString
            ?? "Host=localhost;Port=5432;Username=dev_user;Password=dev_password;Database=devdb";

        var hostCandidates = OperatingSystem.IsAndroid()
            ? new[] { "10.0.2.2", "host.docker.internal", "localhost", "db" }
            : new[] { "localhost", "host.docker.internal", "10.0.2.2", "db" };

        var candidates = new List<string>();

        AddConnectionStringCandidate(candidates, envConnectionString);
        AddConnectionStringCandidate(candidates, defaultConnectionString);

        foreach (var baseConnectionString in new[] { envConnectionString, defaultConnectionString })
        {
            if (string.IsNullOrWhiteSpace(baseConnectionString))
            {
                continue;
            }

            foreach (var host in hostCandidates)
            {
                var candidateBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
                {
                    Host = host,
                    Port = 5432
                };

                AddConnectionStringCandidate(candidates, candidateBuilder.ConnectionString);
            }
        }

        foreach (var candidate in candidates)
        {
            if (CanReachPostgres(candidate))
            {
                return candidate;
            }
        }

        return defaultConnectionString;
    }

    private static void AddConnectionStringCandidate(List<string> candidates, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        if (!candidates.Contains(connectionString, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(connectionString);
        }
    }

    private static string? GetConfiguredConnectionString()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("StarterApp.Database.appsettings.json");
        if (stream is null)
        {
            return null;
        }

        var config = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        return config.GetConnectionString("DevelopmentConnection");
    }

    private static bool CanReachPostgres(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.Host))
            {
                return false;
            }

            var port = builder.Port > 0 ? builder.Port : 5432;
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(builder.Host, port);
            return connectTask.Wait(TimeSpan.FromSeconds(1)) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Item> Items { get; set; }
    public DbSet<RentalRequest> RentalRequests { get; set; }
    public DbSet<Review> Reviews { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Entity configuration keeps constraints/indexes explicit and close to the model contract.
        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.PasswordSalt).HasMaxLength(255);
        });

        // Configure Role entity
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Configure UserRole entity
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();

            entity.HasOne(ur => ur.User)
                  .WithMany(u => u.UserRoles)
                  .HasForeignKey(ur => ur.UserId);

            entity.HasOne(ur => ur.Role)
                  .WithMany(r => r.UserRoles)
                  .HasForeignKey(ur => ur.RoleId);
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasIndex(i => i.OwnerUserId);
            entity.Property(i => i.Title).HasMaxLength(150);
            entity.Property(i => i.Description).HasMaxLength(2000);
            entity.Property(i => i.Category).HasMaxLength(100);
            entity.Property(i => i.Location).HasMaxLength(200);
            entity.Property(i => i.DailyRate).HasPrecision(10, 2);

            entity.HasOne(i => i.OwnerUser)
                  .WithMany(u => u.OwnedItems)
                  .HasForeignKey(i => i.OwnerUserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RentalRequest>(entity =>
        {
            entity.HasIndex(r => r.ItemId);
            entity.HasIndex(r => r.RequestorUserId);
            
            entity.HasOne(r => r.Item)
                  .WithMany(i => i.RentalRequests)
                  .HasForeignKey(r => r.ItemId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.RequestorUser)
                  .WithMany(u => u.RentalRequests)
                  .HasForeignKey(r => r.RequestorUserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

          modelBuilder.Entity<Review>(entity =>
          {
            entity.HasIndex(r => r.ItemId);
            entity.HasIndex(r => r.ReviewerUserId);
            entity.Property(r => r.Comment).HasMaxLength(1000);

            entity.HasOne(r => r.Item)
                .WithMany(i => i.Reviews)
                .HasForeignKey(r => r.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.ReviewerUser)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.ReviewerUserId)
                .OnDelete(DeleteBehavior.Cascade);
          });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(rt => rt.TokenHash).IsUnique();
            entity.HasIndex(rt => rt.UserId);
            entity.Property(rt => rt.TokenHash).HasMaxLength(128);

            entity.HasOne(rt => rt.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

}