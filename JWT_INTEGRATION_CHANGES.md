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

## Item Management (JWT API Path) - Completed

The following Pass requirement is now implemented using JWT-authenticated API calls:

- Create item listing with title, description, daily rate, category, location
- View list of all items
- View detailed item information
- Update item details (owner only)

### API endpoints implemented

- `GET /items` - list active items
- `GET /items/{id}` - item details
- `POST /items` - create item (requires JWT)
- `PUT /items/{id}` - update item (requires JWT, owner only)

Files:
- StarterApp.Api/Program.cs
- StarterApp.Api/ItemDtos.cs

### Database/model changes for items

- Added `Item` model with owner relationship and listing fields.
- Wired `DbSet<Item>` and EF configuration.
- Added migration for item table.

Files:
- StarterApp.Database/Models/Item.cs
- StarterApp.Database/Data/AppDbContext.cs
- StarterApp.Database/Migrations/20260317193000_AddItemsAndRefreshTokens.cs
- StarterApp.Database/Migrations/AppDbContextModelSnapshot.cs

### MAUI client changes (JWT + API)

- Added JWT-backed auth service implementing `IAuthenticationService`.
- App startup now initializes auth state from secure storage.
- Added `IItemApiService` and `ItemApiService` for item endpoint calls.
- Migrated item ViewModels from direct `AppDbContext` access to API service calls.
- Item edit permission in UI now aligns with API owner check.

Files:
- StarterApp/Services/JWTAuthenticationService.cs
- StarterApp/Services/IAuthenticationService.cs
- StarterApp/Services/IItemApiService.cs
- StarterApp/Services/ItemApiService.cs
- StarterApp/ViewModels/ItemListViewModel.cs
- StarterApp/ViewModels/ItemDetailViewModel.cs
- StarterApp/Views/ItemListPage.xaml
- StarterApp/Views/ItemDetailPage.xaml
- StarterApp/MauiProgram.cs
- StarterApp/App.xaml.cs
- StarterApp/StarterApp.csproj

### Runtime requirements (important)

For emulator login and protected item create/update calls to work:

1. API must be running on port 8080.
2. Android cleartext HTTP must be enabled (`android:usesCleartextTraffic="true"`).
3. Emulator tunnel must be active:
  - `adb reverse tcp:8080 tcp:8080`

### Validation status

- `StarterApp.Api` builds successfully.
- `StarterApp` (`net9.0-android`) builds successfully.
- Item feature flow uses API contracts and JWT auth for protected operations.

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

## Demo Guide: How To Prove Endpoints

Use this section during a live demo to prove the backend is working end-to-end.

### Prerequisites

- API running on port `8080`
- For emulator app traffic: `adb reverse tcp:8080 tcp:8080`
- Optional: `jq` installed for pretty JSON output

### 1) Health check (no auth)

Command:

```bash
curl -i http://localhost:8080/health
```

Expected:
- HTTP `200 OK`

### 2) Register a user

Command:

```bash
curl -s -X POST http://localhost:8080/auth/register \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Demo","lastName":"User","email":"demo.user@example.com","password":"Test1234!"}'
```

Expected:
- Success response (or conflict/message that user already exists if rerun)

### 3) Login and capture tokens

Command:

```bash
curl -s -X POST http://localhost:8080/auth/token \
  -H "Content-Type: application/json" \
  -d '{"email":"demo.user@example.com","password":"Test1234!"}'
```

Expected:
- JSON containing: `accessToken`, `refreshToken`, `expiresAtUtc`

### 4) Call an authorized endpoint with access token

Command:

```bash
curl -s http://localhost:8080/auth/me \
  -H "Authorization: Bearer <ACCESS_TOKEN_FROM_LOGIN>"
```

Expected:
- Authenticated user payload (id, email, names, roles)

### 5) Refresh token rotation

Command:

```bash
curl -s -X POST http://localhost:8080/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<REFRESH_TOKEN_FROM_LOGIN>"}'
```

Expected:
- New `accessToken` and new `refreshToken`

### 6) Negative proof (security)

Command (invalid token):

```bash
curl -i http://localhost:8080/auth/me \
  -H "Authorization: Bearer invalid-token"
```

Expected:
- HTTP `401 Unauthorized`

### Swagger UI demo option

If you prefer UI demo instead of curl, open:

- `http://localhost:8080/swagger`

Then run endpoints in order:
1. `/auth/register`
2. `/auth/token`
3. Authorize with bearer token
4. `/auth/me`
5. `/auth/refresh`

### Suggested 3-minute demo script

1. Show `/health` returning 200.
2. Register demo user.
3. Login and point out token payload.
4. Call `/auth/me` with bearer token.
5. Call `/auth/refresh` and show token changes.
6. Show 401 with invalid token to prove auth enforcement.

## Presenter Notes (Plain-English)

Use this script if you are not confident explaining C# details.

### What was changed overall

- We moved authentication from local app-only logic to a proper backend API.
- The app now sends credentials to the API and receives JWT tokens.
- The app stores tokens securely and refreshes them when they are near expiry.
- Protected endpoints now require a valid bearer token.

### How login works now (simple flow)

1. User enters email/password in app.
2. App calls `POST /auth/token`.
3. API validates credentials and returns:
   - `accessToken` (short-lived)
   - `refreshToken` (longer-lived)
4. App stores tokens in secure storage.
5. App includes `Authorization: Bearer <accessToken>` on protected calls.
6. If access token expires, app calls `POST /auth/refresh` and retries.

### Why this is better than local auth

- Centralized security rules on server.
- Tokens allow secure API authorization.
- Refresh flow improves user experience (fewer forced logins).
- Matches real production architecture.

### What errors happened and how they were fixed

- API not reachable from emulator:
  - fixed URL/routing, enabled emulator reverse tunnel, enabled Android cleartext HTTP.
- API DB startup failure (`Host=db` outside compose DNS):
  - added safer connection fallback logic.
- Migration conflict (`role` table already exists):
  - added migration recovery for legacy schema state.
- UI resource crash (`Gray700` missing):
  - added missing color key to shared resources.

## Code Quality Notes (KISS, Clean Code, Smell Check)

### What currently aligns with standards

- KISS:
  - clear API contract (`/auth/token`, `/auth/refresh`, `/auth/me`).
  - straightforward token lifecycle in one service.
- Separation of concerns:
  - UI in ViewModels/pages, auth workflow in auth service, token issuance in API service.
- Extensibility:
  - auth behavior behind interfaces (`IJWTAuthenticationService`).
- Defensive operations:
  - connection fallback and migration recovery added for unstable environments.

### Current quality risks / code smells to acknowledge honestly

- Build warnings still exist (nullable warnings, XAML binding warnings, obsolete API usage).
- Some ViewModels still have large responsibilities and can be split further.
- User/domain features outside auth are still partly direct-DB and not fully API-first.

### Recommended cleanup plan (good for report)

1. Reduce nullable warnings by constructor/field cleanup.
2. Add `x:DataType` in XAML bindings to reduce binding warnings and improve performance.
3. Move remaining direct `DbContext` usage behind repositories/services.
4. Add focused unit tests for JWT service and auth endpoint integration.
5. Track coverage and target >60 percent for Merit criteria.

## Short "Demo Defense" Answers

Use these if asked technical questions in demo:

- "How is auth secure?"
  - Passwords are verified server-side, API issues signed JWTs, protected endpoints require bearer token, refresh tokens are stored server-side and rotated.
- "How do you prove auth is working?"
  - Show `/auth/token` returns token pair, `/auth/me` works with valid token, returns 401 with invalid token, `/auth/refresh` returns new tokens.
- "How do you show engineering quality?"
  - Explain MVVM + interface-driven services + API boundaries + documented troubleshooting + repeatable endpoint demo steps.

## Fixes on March 26, 2026

- **Database Migration Desync Resolved:** Addressed an issue where EF Core believed the database was fully up to date, but the `refresh_token` and `item` tables were missing. This caused the API to hard crash (HTTP 500) during token generation and saving after successful password validation. Manually reconstructed the tables in Postgres matching the orphaned Entity Framework mappings.
- **Port Matching \& ADB Reverse Routing Setup:** Confirmed the API required running on a port visible to the Android Emulator. Used `ASPNETCORE_URLS="http://0.0.0.0:8080"` and `adb reverse tcp:8080 tcp:8080` to solve the "unable to reach auth server" `HttpRequestException` in MAUI.
- **Removed Orphaned Migrations:** Addressed an issue where `.cs` migration files were tracked but their associated `.Designer.cs` metadata files were missing, which caused .NET Core to silently completely ignore pending migrations.

- **MAUI Dark Mode UI Crash Fixed:** Added missing `Gray700` and `Gray800` static color definitions and brushes to `StarterApp/Resources/Styles/Colors.xaml` which prevented an immediate app crash upon successful logical login and redirection to the MainPage. 
- **Automated Startup Integration:** Implemented VS Code tasks (`.vscode/tasks.json` with `runOn: folderOpen`) and Dev Container lifecycle hooks (`postStartCommand` in `.devcontainer.json`) to automatically execute the local `.NET API` bind and `adb reverse` network bridge. This ensures database connectivity seamlessly survives cold boots.

- **Robust Token Auto-Refresh Lifecycle:** Created an `AuthenticationInterceptor` (`DelegatingHandler`) and swapped the monolithic Singleton `HttpClient` over to `IHttpClientFactory` implementations (`AuthClient`, `ApiClient`).
    - **Proactive Refresh:** Automatically intercepts all outbound requests and evaluates token expiry, refreshing before the request ever reaches the server.
    - **Reactive Refresh:** Catches unexpected `401 Unauthorized` responses and fires `ForceRefreshTokenAsync()`, replacing the token and cleanly resubmitting the original HTTP request once under the hood, completely transparent to the user.
    - **Clean Dependency Injection:** Factored token logic out of standard data services (`ItemApiService`), allowing them to focus entirely on domain logic rather than manual token validation.

## Fixes on April 7, 2026

- **ADB Proxy Resiliency:** Updated `adb-proxy.py` to target multiple fallback IP addresses for `host.docker.internal` and provide clearer diagnostic error messages when the remote daemon refuses the connection.
- **Android Theme Crash Fixed:** Resolved a fatal `IllegalArgumentException` on Android app launch ("This component requires that you specify a valid TextAppearance attribute") by creating the missing `StarterApp/Platforms/Android/Resources/values/styles.xml`. Explicitly configured `Maui.SplashTheme` and `Maui.MainTheme.NoActionBar` to inherit from `Theme.MaterialComponents` as requested by the MAUI Android platform interop.
- **API Port Forwarding:** Reinvoked the `adb reverse tcp:8080 tcp:8080` tunnel to allow the Android emulator loopback adapter to successfully reach the running host `.NET API` at `http://localhost:8080`.
