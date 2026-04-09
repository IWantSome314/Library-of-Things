# StarterApp Environment and JWT Integration Log

Last updated: 2026-04-09

## Current Status

- API startup is working in the development container.
- Database migrations now complete successfully against the current legacy dev database.
- JWT authentication endpoints are working.
- Item list, detail, create, and owner-only update endpoints are working.
- Users can browse other people's listings, switch between browse and my-listings views, and submit rental requests.
- Incoming and outgoing rental request lists are available through the API and MAUI client.
- ADB connectivity from the container to the emulator is working, including `adb devices` and `adb reverse tcp:8080 tcp:8080`.

## Changes Made

### 2026-04-09

#### Environment startup and compose

- Updated `.devcontainer/devcontainer.json` so the dev container starts `app`, `api`, and `db` together.
- Updated `.devcontainer/devcontainer.json` to auto-start `adb-proxy.py` on container start before attempting `adb reverse tcp:8080 tcp:8080`.
- Updated `docker-compose.yml` so the app container uses `ADB_SERVER_SOCKET=tcp:127.0.0.1:5037` instead of `tcp:host.docker.internal:5037`.
- Updated `docker-compose.yml` so the app service explicitly depends on the API service.

#### API and database startup recovery

- Fixed `StarterApp.Database/Data/AppDbContext.cs` so `CONNECTION_STRING` is only used directly when the configured Postgres host is actually reachable.
- Added fallback candidate probing so local dev runs can recover from unreachable `Host=db` values outside the compose DNS network.
- Added shared legacy schema repair in `StarterApp.Database/Data/LegacySchemaRepair.cs`.
- Updated `StarterApp.Api/Program.cs` to run legacy schema repair before migrations during API startup.
- Updated `StarterApp.Migrations/Program.cs` to run the same repair logic before manual migration runs.
- Updated `StarterApp.Database/Migrations/20260317193000_AddItemsAndRefreshTokens.cs` so the migration can coexist with an older database that already contains `refresh_token` but does not yet contain `item`.
- Added the missing shared `OutlineButtonStyle` in `StarterApp/Resources/Styles/Styles.xaml` so `RentalRequestPage` can open without crashing when `Request to Rent` is tapped.
- Added missing rental-request API endpoints and connected the listings UI so users can browse other users' items and submit requests from the app.
- Updated the dashboard header copy to welcome the user to Library of Things and replaced the email subtitle with a one-line product description.
- Shortened the dashboard hero text, renamed the two dashboard cards to `Active Listings` and `My Rentals`, and added more seeded fake listings for browsing.
- Tightened the dashboard text further to fit small screens better, renamed the listings card to `Browse Items`, and changed fake listing seeding so existing databases are topped up instead of being skipped.
- Replaced free-text item category entry with a predefined dropdown so listings use a controlled set of categories.
- Fixed rental request submissions returning HTTP 500 by normalizing request dates to UTC before EF writes them to PostgreSQL `timestamp with time zone` columns.
- Added deterministic owner test accounts with known passwords and reassigned the seeded sample listings to those accounts so approval testing can be done by logging in as the listing owner.
- Updated the `My Rentals` screen to use `My Listings` and `Requests` tabs, and expanded request cards to show the requester, price, and note left by the user.
- Added owner actions to approve or deny pending requests and split approved requests into an `Active Rentals` section on the `My Rentals` screen.

## Errors Found and Resolution

### 2026-04-09: API process crashed on startup with unresolved database host

Error:

```text
System.Net.Sockets.SocketException: Name or service not known
server 'tcp://db:5432'
```

Root cause:

- The VS Code task runs `dotnet run` inside the dev container.
- In that context, `CONNECTION_STRING=Host=db...` was inherited even when `db` was not resolvable from the active runtime path.

Resolution:

- Connection selection now verifies reachability before trusting the environment variable and falls back to reachable hosts.

### 2026-04-09: API container missing from the dev environment

Observed behavior:

- `docker ps` on the host showed `app` and `db`, but not `api`.

Root cause:

- The devcontainer was attached to the `app` service and did not explicitly request `api` as a startup service.

Resolution:

- Added `runServices` to `.devcontainer/devcontainer.json` so `api` is part of the environment startup set.

### 2026-04-09: Migrations failed against the existing dev database

Errors observed:

```text
42P01: relation "item" does not exist
42P01: relation "refresh_token" does not exist
```

Root cause:

- The live database contained older migration-history rows and a legacy `refresh_token` table.
- Current code expected the consolidated `AddItemsAndRefreshTokens` migration, but the existing schema reflected an earlier migration chain.

Resolution:

- Added shared schema normalization before EF migration execution.
- Repaired the database state so the current migration chain is now:
  - `20260210141124_InitialCreate`
  - `20260317193000_AddItemsAndRefreshTokens`
  - `20260326161019_AddRentalRequests`

### 2026-04-09: ADB proxy path initially failed from the container

Errors observed:

```text
proxy connection failed: host.docker.internal:5037 -> [Errno 111] Connection refused
hint: host adb is reachable but not accepting remote connections
```

Initial state:

- The container-side socket path has been corrected to use the local proxy port.
- The proxy listener is reachable on `127.0.0.1:5037` inside the container.
- The remaining failure at that point was that the host ADB daemon was not accepting remote connections.

Required host command:

```bash
adb kill-server
adb -a start-server
```

Without `-a`, the proxy cannot forward ADB traffic from the container to the host daemon.

Resolved state:

- After running the host command above, the container can reach the emulator successfully.
- `adb devices` now returns `emulator-5554` from inside the container.
- `adb reverse tcp:8080 tcp:8080` is active and the MAUI app can use `http://localhost:8080` through the emulator tunnel.

## Validation Performed

### 2026-04-09 API validation

Successful checks:

- `GET /health` returned `200 OK`
- `POST /auth/register` returned `200 OK`
- `POST /auth/token` returned `200 OK`
- `GET /auth/me` with bearer token returned `200 OK`
- `POST /auth/refresh` returned `200 OK`
- `GET /items` returned `200 OK`
- `GET /items/{id}` returned `200 OK`
- `POST /items` with bearer token returned `201 Created`
- `PUT /items/{id}` by the owner returned `200 OK`
- `PUT /items/{id}` by a different user returned `403 Forbidden`

### 2026-04-09 database validation

Confirmed tables present:

- `users`
- `role`
- `user_role`
- `refresh_token`
- `item`
- `rental_request`
- `__EFMigrationsHistory`

## Goal Review

### User Authentication Integration

Status: complete in backend and client wiring, validated at API level.

Confirmed:

- `POST /auth/token` issues JWT access and refresh tokens.
- Refresh token flow works through `POST /auth/refresh`.
- Client interceptor wiring is present and attaches bearer tokens to authenticated API calls.
- Client auth service persists tokens and refreshes near expiry.
- API can now start successfully in the dev environment once the container opens.

Files involved:

- `StarterApp.Api/Program.cs`
- `StarterApp.Api/TokenService.cs`
- `StarterApp/Services/JWTAuthenticationService.cs`
- `StarterApp/Services/AuthenticationInterceptor.cs`
- `StarterApp/MauiProgram.cs`

### Item Management

Status: complete in backend and validated by smoke test.

Confirmed:

- Create item listing with title, description, daily rate, category, and location.
- View list of all items.
- View detailed item information.
- Update item details as owner.
- Non-owner update attempts are rejected with `403 Forbidden`.

Files involved:

- `StarterApp.Api/Program.cs`
- `StarterApp.Api/ItemDtos.cs`
- `StarterApp/Services/IItemApiService.cs`
- `StarterApp/Services/ItemApiService.cs`
- `StarterApp/ViewModels/ItemListViewModel.cs`
- `StarterApp/ViewModels/ItemDetailViewModel.cs`

## Detailed Integration Notes

### What Was Added

#### New API project

- Added `StarterApp.Api` as an ASP.NET Core Web API project.
- Added endpoints:
  - `POST /auth/token`
  - `POST /auth/register`
  - `POST /auth/refresh`
  - `GET /auth/me` (authorized)
  - `POST /auth/change-password` (authorized)
  - `GET /health`
- Added startup authentication and authorization middleware.
- Added automatic database migration on API startup.
- Added role seeding if no roles exist.

Files:

- `StarterApp.Api/StarterApp.Api.csproj`
- `StarterApp.Api/Program.cs`
- `StarterApp.Api/AuthDtos.cs`
- `StarterApp.Api/JwtOptions.cs`
- `StarterApp.Api/TokenService.cs`
- `StarterApp.Api/appsettings.json`

#### Refresh token persistence in database

- Added `RefreshToken` model and EF configuration.
- Added `DbSet` for refresh tokens.
- Added migration and updated model snapshot.

Files:

- `StarterApp.Database/Models/RefreshToken.cs`
- `StarterApp.Database/Data/AppDbContext.cs`
- `StarterApp.Database/Migrations/20260317172501_AddRefreshTokens.cs`
- `StarterApp.Database/Migrations/20260317172501_AddRefreshTokens.Designer.cs`
- `StarterApp.Database/Migrations/AppDbContextModelSnapshot.cs`

#### MAUI client JWT lifecycle updates

- Extended auth interface with startup initialization and token validity API.
- Reworked `JWTAuthenticationService` to:
  - store access and refresh tokens in secure storage
  - load tokens on app startup
  - refresh access token automatically near expiry
  - retry authenticated calls after `401` for protected operations
  - clear all persisted token state on logout
- Triggered auth service initialization from app startup.

Files:

- `StarterApp/Services/IJWTAuthenticationService.cs`
- `StarterApp/Services/JWTAuthenticationService.cs`
- `StarterApp/App.xaml.cs`

#### Environment and compose wiring

- Added API service to `docker-compose.yml`.
- Exposed API on port `8080`.
- Added auth API base URL for app service.
- Added JWT environment variables for API.

File:

- `docker-compose.yml`

#### Solution and documentation updates

- Added `StarterApp.Api` to the solution.
- Updated README with JWT architecture, endpoint list, and run instructions.

Files:

- `StarterApp.sln`
- `README.md`

### Build and Validation Status

Succeeded:

- `StarterApp.Api`
- `StarterApp.Database`
- `StarterApp.Migrations`
- `StarterApp` (`net9.0-android`) with existing warnings

Historical note:

- Earlier in this container session, Docker CLI was not available from the container shell, so compose runtime validation could not be executed there directly.

### Runtime Contract Used By MAUI

- Login request: `POST /auth/token` with email and password.
- Login response: `accessToken`, `refreshToken`, `expiresAtUtc`.
- Refresh request: `POST /auth/refresh` with `refreshToken`.
- Protected requests must include `Authorization: Bearer <accessToken>`.

### Known Follow-up Work

- User management view models still access `DbContext` directly and should be moved to API endpoints for full API-only architecture.
- Consider implementing refresh token cleanup and stronger revocation or reuse handling.
- Add automated integration tests for token issuance and refresh behavior.

## Item Management (JWT API Path) - Completed

The following Pass requirement is implemented using JWT-authenticated API calls:

- Create item listing with title, description, daily rate, category, and location.
- View list of all items.
- View detailed item information.
- Update item details (owner only).

### API endpoints implemented

- `GET /items` - list active items
- `GET /items/{id}` - item details
- `POST /items` - create item (requires JWT)
- `PUT /items/{id}` - update item (requires JWT, owner only)
- `POST /rentals` - create a rental request for another user's item (requires JWT)
- `GET /rentals/incoming` - list requests received for your items (requires JWT)
- `GET /rentals/outgoing` - list requests you have submitted (requires JWT)

Files:

- `StarterApp.Api/Program.cs`
- `StarterApp.Api/ItemDtos.cs`
- `StarterApp.Api/RentalDtos.cs`

### Database and model changes for items

- Added `Item` model with owner relationship and listing fields.
- Wired `DbSet<Item>` and EF configuration.
- Added migration for item table.

Files:

- `StarterApp.Database/Models/Item.cs`
- `StarterApp.Database/Data/AppDbContext.cs`
- `StarterApp.Database/Migrations/20260317193000_AddItemsAndRefreshTokens.cs`
- `StarterApp.Database/Migrations/AppDbContextModelSnapshot.cs`

### MAUI client changes (JWT and API)

- Added JWT-backed auth service implementing `IAuthenticationService`.
- App startup now initializes auth state from secure storage.
- Added `IItemApiService` and `ItemApiService` for item endpoint calls.
- Added `IRentalApiService` and `RentalApiService` for rental request endpoints.
- Migrated item view models from direct `AppDbContext` access to API service calls.
- Item edit permission in UI now aligns with API owner check.
- Item list now supports a `Browse Listings` view for other users' items and a `My Listings` view for the current user's items.
- Browse rows now expose a direct `Request` action for non-owner items.

Files:

- `StarterApp/Services/JWTAuthenticationService.cs`
- `StarterApp/Services/IAuthenticationService.cs`
- `StarterApp/Services/IItemApiService.cs`
- `StarterApp/Services/ItemApiService.cs`
- `StarterApp/Services/IRentalApiService.cs`
- `StarterApp/Services/RentalApiService.cs`
- `StarterApp/ViewModels/ItemListViewModel.cs`
- `StarterApp/ViewModels/ItemDetailViewModel.cs`
- `StarterApp/ViewModels/RentalListViewModel.cs`
- `StarterApp/ViewModels/RentalRequestViewModel.cs`
- `StarterApp/Views/ItemListPage.xaml`
- `StarterApp/Views/ItemDetailPage.xaml`
- `StarterApp/Views/RentalListPage.xaml`
- `StarterApp/Views/RentalRequestPage.xaml`
- `StarterApp/MauiProgram.cs`
- `StarterApp/App.xaml.cs`
- `StarterApp/StarterApp.csproj`

### Runtime requirements (important)

For emulator login and protected item create or update calls to work:

- API must be running on port `8080`.
- Android cleartext HTTP must be enabled with `android:usesCleartextTraffic="true"`.
- Emulator tunnel must be active:

```bash
adb reverse tcp:8080 tcp:8080
```

### Validation status

- `StarterApp.Api` builds successfully.
- `StarterApp` (`net9.0-android`) builds successfully.
- Item feature flow uses API contracts and JWT auth for protected operations.
- Rental request flow is validated at API level:
  - `POST /rentals` returns `201 Created`
  - `GET /rentals/outgoing` returns the submitted request
  - `GET /rentals/incoming` returns the same request for the item owner
- Revalidated rental creation against the current live API after the recent UI changes and it still returns `201 Created`.
- Confirmed the prior `500` on `Submit Request` was caused by `DateTimeKind.Unspecified` values being written into the `rental_request` date columns; the API now converts those values to UTC before saving.

## Post-Integration Troubleshooting and Fixes

### 1) Login failed: cannot reach auth server at `http://10.0.2.2:8080/auth/token`

Symptoms:

- Android login showed `cannot reach auth server`.

Root cause:

- API was not reachable from the app runtime path being used.

Fixes applied:

- Ensured API process is started and listening on `0.0.0.0:8080`.
- Updated MAUI API base URL to use `http://localhost:8080`.
- Configured emulator tunnel with `adb reverse tcp:8080 tcp:8080`.
- Enabled cleartext HTTP on Android by adding `android:usesCleartextTraffic="true"` in the Android manifest.

Files:

- `StarterApp/MauiProgram.cs`
- `StarterApp/Platforms/Android/AndroidManifest.xml`

### 2) API startup failure: database host `db` not resolvable outside compose network

Symptoms:

- API startup failed when run directly in the container session using `dotnet run`.

Root cause:

- `CONNECTION_STRING` pointed to `Host=db`, which only resolves inside Docker Compose service DNS.

Fixes applied:

- Updated connection-string resolution logic to only trust the environment connection string if Postgres is reachable.
- Reordered fallback host probing to prefer reachable hosts in this order: `localhost`, `host.docker.internal`, `10.0.2.2`, then `db`.

File:

- `StarterApp.Database/Data/AppDbContext.cs`

### 3) API migration failure: `42P07 relation "role" already exists`

Symptoms:

- API crashed during startup migrations with existing schema objects.

Root cause:

- Legacy schema existed without complete EF migrations history state.

Fixes applied:

- Added migration recovery flow that:
  - catches Postgres `42P07`
  - bootstraps `__EFMigrationsHistory` if needed
  - inserts the initial migration record idempotently
  - retries migrations

File:

- `StarterApp.Api/Program.cs`

### 4) Runtime UI failure after login: `StaticResource not found for key Gray700`

Symptoms:

- Login or post-login action path failed with an XAML resource error for key `Gray700`.

Root cause:

- `Gray700` was referenced by XAML but not defined in shared color resources.

Fixes applied:

- Added `Gray700` color resource.
- Added `Gray700Brush` resource for consistency.

File:

- `StarterApp/Resources/Styles/Colors.xaml`

### 5) Rental flow crash: `Request to Rent` button closed the app

Symptoms:

- Tapping `Request to Rent` from the item detail screen terminated the Android app.

Root cause:

- `RentalRequestPage.xaml` referenced `OutlineButtonStyle` on the Cancel button.
- That style key did not exist in the shared resource dictionaries, so MAUI threw a `XamlParseException` while constructing `RentalRequestPage`.
- Because the exception happened during Shell route navigation, Android treated it as a fatal unhandled exception and closed the app.

Fixes applied:

- Added a shared `OutlineButtonStyle` to `StarterApp/Resources/Styles/Styles.xaml`.
- Kept the style centralized so the rental request page and any future secondary-action buttons can reuse the same outline appearance safely.

Files:

- `StarterApp/Resources/Styles/Styles.xaml`
- `StarterApp/Views/RentalRequestPage.xaml`

### 6) Rental requests were wired in the client but missing in the API

Symptoms:

- The MAUI client had `RentalApiService`, `RentalListPage`, and `RentalRequestPage`, but request submission and rental list loading could not work end to end.
- Direct API checks for `/rentals`, `/rentals/incoming`, and `/rentals/outgoing` returned `404 Not Found` before the fix.

Root cause:

- The backend had rental DTOs and database models, but the minimal API endpoints themselves had not been added to `StarterApp.Api/Program.cs`.

Fixes applied:

- Added `POST /rentals` to create rental requests for non-owner items.
- Added `GET /rentals/incoming` to list requests for the current user's items.
- Added `GET /rentals/outgoing` to list requests submitted by the current user.
- Calculated `TotalPrice` from item daily rate and requested rental duration.
- Updated the item list UI so users can browse other users' listings and request directly from the list.

Files:

- `StarterApp.Api/Program.cs`
- `StarterApp/ViewModels/ItemListViewModel.cs`
- `StarterApp/Views/ItemListPage.xaml`
- `StarterApp/ViewModels/ItemDetailViewModel.cs`

### 7) Item category input allowed arbitrary text

Symptoms:

- Category could be typed freely when creating or editing an item, which made listings inconsistent and increased the chance of typos.

Root cause:

- `ItemDetailPage` used a plain `Entry` bound to the category string instead of a constrained selection control.

Fixes applied:

- Replaced the free-text category field with a `Picker` backed by a predefined category list.
- Kept support for existing saved categories by adding any unknown category from older data into the picker at runtime.

Files:

- `StarterApp/ViewModels/ItemDetailViewModel.cs`
- `StarterApp/Views/ItemDetailPage.xaml`

### 8) Rental submit returned HTTP 500 for valid requests

Symptoms:

- Pressing `Submit Request` on the rental page showed `API request failed with status 500.` even when the item, dates, auth token, and API tunnel were otherwise valid.

Root cause:

- The rentals endpoint stored `request.StartDate.Date` and `request.EndDate.Date` directly.
- Those values have `DateTimeKind.Unspecified`.
- PostgreSQL was expecting `timestamp with time zone` values for `rental_request.StartDate` and `rental_request.EndDate`, and Npgsql rejects non-UTC `DateTime` values for that column type.

Fixes applied:

- Reproduced the exact `500` against item `500 Test Saw` to confirm it was not a client-only issue.
- Captured the live exception from the running API process.
- Normalized rental dates to `DateTimeKind.Utc` before saving the entity.
- Removed the obsolete `Application.Current.MainPage` success alert usage in the rental request view model while touching the submit path.

Files:

- `StarterApp.Api/Program.cs`
- `StarterApp/ViewModels/RentalRequestViewModel.cs`

### 9) Owner accounts for approval testing were not predictable

Symptoms:

- The sample listings existed, but the accounts behind those listings were a mix of old test users and unknown credentials, so it was difficult to log in as the owner and test the approval flow.

Root cause:

- Seeded listings were attached to whichever legacy user already existed in the database instead of a stable set of known test-owner accounts.

Fixes applied:

- Added deterministic test-owner accounts with fixed emails and the shared password `Password123!`.
- Reassigned the seeded listings to those test-owner accounts on startup.
- Kept the recent requestor account available for end-to-end rent/approve testing.

Verified owner/test logins:
- `hamish@email.com` / `hamish`
- `james@email.com` / `Password123!` for the main seeded listings owner
- `john@email.com` / `Password123!` for the `Projector` owner account
- `ross@email.com` / `Password123!` for `Cordless Jigsaw Pro` and `500 Test Saw`
- `requestor.latest.check@example.com` / `Password123!` for submitting rental requests before switching to an owner account to approve them

Files:

- `StarterApp.Api/Program.cs`

### 10) My Rentals screen did not match the owner workflow

Symptoms:

- The rentals page still used `Incoming` and `Outgoing` tabs, which did not match the owner-focused flow needed for managing listings and incoming requests.
- Owners needed to see the listings they own and, separately, the requests made against those listings with requester and pricing context.

Root cause:

- The page was still structured around generic request direction instead of the actual owner tasks used in the app.

Fixes applied:

- Reworked the rentals page tabs to `My Listings` and `Requests`.
- Loaded the current user's own listings into the first tab.
- Kept incoming requests in the second tab and expanded each request card to show:
  - requester name
  - item title
  - start and end dates
  - total price
  - note/message left by the requester
- Updated the dashboard card description so it reflects listings plus request management.

Files:

- `StarterApp/ViewModels/RentalListViewModel.cs`
- `StarterApp/Views/RentalListPage.xaml`
- `StarterApp/Views/MainPage.xaml`

### 11) Owners could not action requests or review active rentals

Symptoms:

- Owners could see incoming requests, but they had no way to approve or deny them in the app.
- Approved rentals were not surfaced separately, so there was no clear view of currently active rentals.

Root cause:

- The backend only exposed request creation and listing endpoints.
- The MAUI rentals screen displayed request details, but it did not offer status actions or split approved rentals into a dedicated active state view.

Fixes applied:

- Added authenticated owner-only API endpoints to approve or deny a rental request.
- Extended the MAUI rental service with request-status update calls.
- Updated the rentals view model to split incoming requests into `Pending Requests` and `Active Rentals`.
- Added `Approve` and `Deny` buttons for pending requests directly on the rentals page.

Files:

- `StarterApp.Api/Program.cs`
- `StarterApp.Api/RentalDtos.cs`
- `StarterApp/Services/IRentalApiService.cs`
- `StarterApp/Services/RentalApiService.cs`
- `StarterApp/ViewModels/RentalListViewModel.cs`
- `StarterApp/Views/RentalListPage.xaml`

### Final validated state after fixes

- API responds on `/health` with HTTP `200`.
- Android app builds and deploys successfully (`net9.0-android`).
- Registration and login flow are no longer blocked by endpoint reachability or missing resource key issues.
- The `Request to Rent` navigation path no longer depends on a missing XAML resource key.
- Users can browse other people's listings, submit rental requests, and load incoming and outgoing requests through the live API.

## Demo Guide: How To Prove Endpoints

Use this section during a live demo to prove the backend is working end to end.

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

- Success response, or a conflict message if rerun.

### 3) Login and capture tokens

Command:

```bash
curl -s -X POST http://localhost:8080/auth/token \
  -H "Content-Type: application/json" \
  -d '{"email":"demo.user@example.com","password":"Test1234!"}'
```

Expected:

- JSON containing `accessToken`, `refreshToken`, and `expiresAtUtc`.

### 4) Call an authorized endpoint with access token

Command:

```bash
curl -s http://localhost:8080/auth/me \
  -H "Authorization: Bearer <ACCESS_TOKEN_FROM_LOGIN>"
```

Expected:

- Authenticated user payload including id, email, names, and roles.

### 5) Refresh token rotation

Command:

```bash
curl -s -X POST http://localhost:8080/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"<REFRESH_TOKEN_FROM_LOGIN>"}'
```

Expected:

- New `accessToken` and new `refreshToken`.

### 6) Negative proof (security)

Command (invalid token):

```bash
curl -i http://localhost:8080/auth/me \
  -H "Authorization: Bearer invalid-token"
```

Expected:

- HTTP `401 Unauthorized`

### Swagger UI demo option

If you prefer UI demo instead of `curl`, open:

- `http://localhost:8080/swagger`

Then run endpoints in order:

- `/auth/register`
- `/auth/token`
- Authorize with bearer token
- `/auth/me`
- `/auth/refresh`

### Suggested 3-minute demo script

- Show `/health` returning `200`.
- Register demo user.
- Login and point out the token payload.
- Call `/auth/me` with bearer token.
- Call `/auth/refresh` and show token changes.
- Show `401` with invalid token to prove auth enforcement.

## Presenter Notes (Plain-English)

Use this script if you are not confident explaining C# details.

### What was changed overall

- Authentication was moved from local app-only logic to a backend API.
- The app now sends credentials to the API and receives JWT tokens.
- The app stores tokens securely and refreshes them when they are near expiry.
- Protected endpoints now require a valid bearer token.

### How login works now (simple flow)

- User enters email and password in the app.
- App calls `POST /auth/token`.
- API validates credentials and returns:
  - `accessToken` (short-lived)
  - `refreshToken` (longer-lived)
- App stores tokens in secure storage.
- App includes `Authorization: Bearer <accessToken>` on protected calls.
- If access token expires, app calls `POST /auth/refresh` and retries.

### Why this is better than local auth

- Centralized security rules on the server.
- Tokens allow secure API authorization.
- Refresh flow improves user experience by reducing forced logins.
- It matches a more realistic production architecture.

### What errors happened and how they were fixed

- API not reachable from emulator:
  - fixed URL and routing
  - enabled emulator reverse tunnel
  - enabled Android cleartext HTTP
- API database startup failure (`Host=db` outside compose DNS):
  - added safer connection fallback logic
- Migration conflict (`role` table already exists):
  - added migration recovery for legacy schema state
- UI resource crash (`Gray700` missing):
  - added the missing color key to shared resources

## Code Quality Notes (KISS, Clean Code, Smell Check)

### What currently aligns with standards

- KISS:
  - clear API contract with `/auth/token`, `/auth/refresh`, and `/auth/me`
  - straightforward token lifecycle in one service
- Separation of concerns:
  - UI in view models and pages
  - auth workflow in auth service
  - token issuance in API service
- Extensibility:
  - auth behavior behind interfaces such as `IJWTAuthenticationService`
- Defensive operations:
  - connection fallback and migration recovery added for unstable environments

### Current quality risks and code smells to acknowledge honestly

- Build warnings still exist, including nullable warnings, XAML binding warnings, and obsolete API usage.
- Some view models still have large responsibilities and can be split further.
- Some user or domain features outside auth are still partly direct-database and not fully API-first.

### Recommended cleanup plan

- Reduce nullable warnings through constructor and field cleanup.
- Add `x:DataType` in XAML bindings to reduce binding warnings and improve performance.
- Move remaining direct `DbContext` usage behind repositories or services.
- Add focused unit tests for the JWT service and auth endpoint integration.
- Track coverage and target greater than `60%` for Merit criteria.

## Short Demo Defense Answers

### How is auth secure?

- Passwords are verified server-side.
- The API issues signed JWTs.
- Protected endpoints require bearer tokens.
- Refresh tokens are stored server-side and rotated.

### How do you prove auth is working?

- Show `/auth/token` returns a token pair.
- Show `/auth/me` works with a valid token.
- Show `/auth/me` returns `401` with an invalid token.
- Show `/auth/refresh` returns new tokens.

### How do you show engineering quality?

- Explain MVVM plus interface-driven services plus API boundaries plus documented troubleshooting plus repeatable endpoint demo steps.

## Historical Fixes

### Fixes on March 26, 2026

- Database migration desync resolved:
  - EF Core believed the database was fully up to date while `refresh_token` and `item` tables were missing.
  - This caused the API to hard crash during token generation after successful password validation.
  - The missing tables were manually reconstructed in Postgres to match the orphaned EF mappings at that time.
- Port matching and ADB reverse routing setup:
  - Confirmed the API needed to run on a port visible to the Android emulator.
  - Used `ASPNETCORE_URLS="http://0.0.0.0:8080"` and `adb reverse tcp:8080 tcp:8080` to solve the auth reachability issue.
- Removed orphaned migrations:
  - Earlier migration `.cs` files existed without matching `.Designer.cs` metadata files, which caused EF migration discovery problems.
- MAUI dark mode UI crash fixed:
  - Added missing `Gray700` and `Gray800` colors and brushes to `StarterApp/Resources/Styles/Colors.xaml` to prevent crashes after login and redirect.
- Automated startup integration:
  - Implemented VS Code tasks and dev container lifecycle hooks to start the local API and `adb reverse` bridge automatically.
- Robust token auto-refresh lifecycle:
  - Created `AuthenticationInterceptor` as a `DelegatingHandler`.
  - Switched to `IHttpClientFactory` clients (`AuthClient`, `ApiClient`).
  - Added proactive refresh before requests.
  - Added reactive refresh on unexpected `401 Unauthorized` responses.
  - Kept domain services such as `ItemApiService` focused on domain logic rather than token plumbing.

### Fixes on April 7, 2026

- ADB proxy resiliency:
  - Updated `adb-proxy.py` to target multiple fallback host addresses and provide clearer diagnostic messages.
- Android theme crash fixed:
  - Resolved the Android launch crash related to `TextAppearance` by creating `StarterApp/Platforms/Android/Resources/values/styles.xml` and configuring MAUI themes to inherit from `Theme.MaterialComponents`.
- API port forwarding:
  - Reinvoked `adb reverse tcp:8080 tcp:8080` so the Android emulator could reach the host API at `http://localhost:8080`.

## What You Need To Do Next

### Current practical next steps

- Rebuild or reopen the dev container if you want the updated `.devcontainer/devcontainer.json` startup settings to apply automatically on a fresh session.
- If host ADB is reset again in future, rerun:

```bash
adb kill-server
adb -a start-server
```
