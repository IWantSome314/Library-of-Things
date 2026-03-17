# JWT Integration Change Log

Date: 2026-03-17

## Summary

StarterApp was converted from local-only authentication flow to API-driven JWT authentication with refresh token rotation.

## What Was Added

### New API project

- Added StarterApp.Api as an ASP.NET Core Web API project.
- Added endpoints:
  - POST /auth/token
  - POST /auth/register
  - POST /auth/refresh
  - GET /auth/me (authorized)
  - POST /auth/change-password (authorized)
  - GET /health
- Added startup authentication and authorization middleware.
- Added automatic database migration on API startup.
- Added role seeding if no roles exist.

Files:
- StarterApp.Api/StarterApp.Api.csproj
- StarterApp.Api/Program.cs
- StarterApp.Api/AuthDtos.cs
- StarterApp.Api/JwtOptions.cs
- StarterApp.Api/TokenService.cs
- StarterApp.Api/appsettings.json

### Refresh token persistence in database

- Added RefreshToken model and EF configuration.
- Added DbSet for refresh tokens.
- Added migration and updated model snapshot.

Files:
- StarterApp.Database/Models/RefreshToken.cs
- StarterApp.Database/Data/AppDbContext.cs
- StarterApp.Database/Migrations/20260317172501_AddRefreshTokens.cs
- StarterApp.Database/Migrations/20260317172501_AddRefreshTokens.Designer.cs
- StarterApp.Database/Migrations/AppDbContextModelSnapshot.cs

### MAUI client JWT lifecycle updates

- Extended auth interface with startup initialization and token validity API.
- Reworked JWTAuthenticationService to:
  - store access and refresh tokens in secure storage
  - load tokens on app startup
  - refresh access token automatically near expiry
  - retry authenticated call after 401 for protected operations
  - clear all persisted token state on logout
- Triggered auth service initialization from App startup.

Files:
- StarterApp/Services/IJWTAuthenticationService.cs
- StarterApp/Services/JWTAuthenticationService.cs
- StarterApp/App.xaml.cs

### Environment and compose wiring

- Added API service to docker-compose.
- Exposed API on port 8080.
- Added auth API base URL for app service.
- Added JWT environment variables for API.

File:
- docker-compose.yml

### Solution and documentation updates

- Added StarterApp.Api to solution.
- Updated README with JWT architecture, endpoint list, and run instructions.

Files:
- StarterApp.sln
- README.md

## Build and Validation Status

Succeeded:
- StarterApp.Api
- StarterApp.Database
- StarterApp.Migrations
- StarterApp (net9.0-android) with existing warnings

Note:
- Docker CLI was not available in this container session, so compose runtime validation could not be executed here.

## Runtime Contract Used By MAUI

- Login request: POST /auth/token with email and password.
- Login response: accessToken, refreshToken, expiresAtUtc.
- Refresh request: POST /auth/refresh with refreshToken.
- Protected requests must include Authorization: Bearer <accessToken>.

## Known Follow-up Work

- User management view models still access DbContext directly and should be moved to API endpoints for full API-only architecture.
- Consider implementing refresh token cleanup and stronger revocation/reuse handling.
- Add automated integration tests for token issue and refresh behavior.

## Post-Integration Troubleshooting and Fixes

### 1) Login failed: cannot reach auth server at http://10.0.2.2:8080/auth/token

Symptoms:
- Android login showed: "cannot reach auth server".

Root cause:
- API was not reachable from the app runtime path being used.

Fixes applied:
- Ensured API process is started and listening on `0.0.0.0:8080`.
- Updated MAUI API base URL to use `http://localhost:8080`.
- Configured emulator tunnel with `adb reverse tcp:8080 tcp:8080`.
- Enabled cleartext HTTP on Android by adding `android:usesCleartextTraffic="true"` in Android manifest.

Files:
- StarterApp/MauiProgram.cs
- StarterApp/Platforms/Android/AndroidManifest.xml

### 2) API startup failure: database host "db" not resolvable outside compose network

Symptoms:
- API startup failed when run directly in container session using `dotnet run`.

Root cause:
- `CONNECTION_STRING` environment value pointed to `Host=db`, which only resolves inside Docker Compose service DNS.

Fixes applied:
- Updated connection-string resolution logic to only trust env connection string if Postgres is reachable.
- Reordered fallback host probing to prefer local reachable hosts first (`localhost`, `host.docker.internal`, `10.0.2.2`, then `db`).

File:
- StarterApp.Database/Data/AppDbContext.cs

### 3) API migration failure: `42P07 relation "role" already exists`

Symptoms:
- API crashed during startup migrations with existing schema objects.

Root cause:
- Legacy schema existed without complete EF migrations history state.

Fixes applied:
- Added migration recovery flow:
  - catches Postgres `42P07`
  - bootstraps `__EFMigrationsHistory` if needed
  - inserts initial migration record (idempotent)
  - retries migrations

File:
- StarterApp.Api/Program.cs

### 4) Runtime UI failure after login: `StaticResource not found for key Gray700`

Symptoms:
- Login/action path failed with XAML resource error for key `Gray700`.

Root cause:
- `Gray700` was referenced by XAML but not defined in shared color resources.

Fixes applied:
- Added `Gray700` color resource.
- Added `Gray700Brush` resource for consistency.

File:
- StarterApp/Resources/Styles/Colors.xaml

### Final validated state after fixes

- API responds on `/health` with HTTP 200.
- Android app builds and deploys successfully (`net9.0-android`).
- Registration and login flow no longer blocked by endpoint reachability or missing resource key issues.
