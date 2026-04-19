# StarterApp — Presentation Evidence Guide

Last updated: 2026-04-19

This document lists every assessed objective and explains exactly how to demonstrate or prove it works during a presentation or viva.

---

## Objective 1 — User Authentication Integration

**What it covers:** Obtaining a JWT from the API, storing it, attaching it to requests, and refreshing it when it expires.

### How to prove it works

#### Live demo steps
1. Launch the app on the Android emulator.
2. Enter credentials on the Login screen and tap **Login**.
3. The app calls `POST /auth/token` on the API. A valid JWT and refresh token are returned and stored in-memory via `JWTAuthenticationService`.
4. Navigate to any authenticated screen (Items, Rentals, Profile). Each request silently attaches the bearer token via the `AuthenticationInterceptor` DelegatingHandler.
5. To show token refresh: in `JWTAuthenticationService.cs`, the `RefreshTokenAsync` method is called automatically when a 401 is received — no user action required.

#### API calls to show
```
POST http://localhost:8080/auth/token
Body: { "email": "...", "password": "..." }
Response: { "token": "...", "refreshToken": "..." }

POST http://localhost:8080/auth/refresh
Body: { "refreshToken": "..." }
Response: { "token": "...", "refreshToken": "..." }
```

#### Key files to open
- `StarterApp/Services/JWTAuthenticationService.cs` — token issue and refresh logic
- `StarterApp/Services/AuthenticationInterceptor.cs` — automatic bearer header injection
- `StarterApp/ViewModels/LoginViewModel.cs` — calls `AuthenticateAsync`
- `StarterApp.Api/Program.cs` — JWT middleware configuration

---

## Objective 2 — Item Management

**What it covers:** Creating item listings, browsing all items, viewing item detail, and updating items as owner only.

### How to prove it works

#### Live demo steps
1. Log in as a regular user. Navigate to **Browse** — all items are fetched via `GET /items`.
2. Tap any item — the detail view loads from `GET /items/{id}`.
3. Log in as the item owner. Navigate to **My Items**. Tap an item — an **Edit** button appears that is absent for non-owners. Tap Edit and save — triggers `PUT /items/{id}` with the owner's token.
4. Tap **Add Item** on the My Items screen. Fill out the form and save — triggers `POST /items`.

#### API calls to show
```
GET  http://localhost:8080/items              — all items (public)
GET  http://localhost:8080/items/{id}         — item detail
POST http://localhost:8080/items              — create (auth required)
PUT  http://localhost:8080/items/{id}         — update (owner token only; 403 for others)
```

#### Key files to open
- `StarterApp.Api/Program.cs` — item endpoints and owner authorization check
- `StarterApp/Services/ItemApiService.cs` — MAUI-side HTTP methods
- `StarterApp/ViewModels/ItemListViewModel.cs` and `ItemDetailViewModel.cs`
- `StarterApp.Database/Data/Repositories/ItemRepository.cs` — data access layer

---

## Objective 3 — Basic Rental Request

**What it covers:** Submitting a rental request for an item; viewing incoming (owner) and outgoing (requester) rental requests in the app UI.

### How to prove it works

#### Live demo steps
1. Log in as User A. Browse to an item owned by User B. Tap **Request Rental**. Fill in start/end dates and submit.
2. The app calls `POST /rentals`. The new request is saved with status `Pending`.
3. Tap the **Rentals** tab in the app → **Requests** sub-tab. Scroll to **Outgoing Requests** — the newly submitted request appears with status `Pending`, the owner's name, dates, and price.
4. Log out. Log in as User B (the owner). Tap the **Rentals** tab → **Requests** sub-tab. The request appears in **Pending Requests**. Tap **Approve** or **Deny**.
5. Switch back to User A. The outgoing request now shows the updated status.

#### API calls to show
```
POST http://localhost:8080/rentals                     — create request (auth required)
GET  http://localhost:8080/rentals/incoming            — owner's incoming requests
GET  http://localhost:8080/rentals/outgoing            — requester's outgoing requests
PUT  http://localhost:8080/rentals/{id}/approve        — owner approves
PUT  http://localhost:8080/rentals/{id}/deny           — owner denies
```

#### Key files to open
- `StarterApp/ViewModels/RentalListViewModel.cs` — `OutgoingRequests`, `PendingRequests`, `ActiveRentals`, `PastRentals` collections
- `StarterApp/Views/RentalListPage.xaml` — four sections in the Requests tab
- `StarterApp/Services/RentalApiService.cs` — `GetOutgoingRequestsAsync`, `GetIncomingRequestsAsync`
- `StarterApp.Database/Data/Repositories/RentalRepository.cs`

---

## Objective 4 — Active vs Past Rental Split

**What it covers:** Approved rentals are automatically categorised as Active (end date in the future) or Past (end date already passed).

### How to prove it works

#### Live demo steps
1. Log in as a user who has approved rentals.
2. Navigate to **Rentals** → **Requests** tab.
3. Rentals whose `EndDate >= today` appear under **Active Rentals**.
4. Rentals whose `EndDate < today` appear under **Past Rentals**.
5. No manual input required — the split is calculated at load time.

#### Where the logic lives
```csharp
// StarterApp/ViewModels/RentalListViewModel.cs
ActiveRentals  = approved where r.EndDate >= DateTime.UtcNow
PastRentals    = approved where r.EndDate <  DateTime.UtcNow
```

#### Key file to open
- `StarterApp/ViewModels/RentalListViewModel.cs` — `LoadAsync` method

---

## Objective 5 — MVVM Architecture

**What it covers:** Every main page has a ViewModel; ViewModels use `ObservableObject`; UI events are Commands; Views do not contain business logic.

### How to prove it works

#### Things to show in code
1. Open any `Views/` XAML file — the `BindingContext` is injected (not newed up inline). No business logic in code-behind.
2. Open a ViewModel (e.g., `ItemListViewModel.cs`) — it extends `BaseViewModel` which extends `ObservableObject` from CommunityToolkit.Mvvm.
3. Point out `[RelayCommand]` attributes generating `ICommand` implementations automatically.
4. Show `IUserNotificationService` — ViewModels never call `DisplayAlert` directly; all UI notifications go through the abstraction.
5. Show `INavigationService` — navigation decisions are in ViewModels, not Views.

#### Key files to open
- `StarterApp/ViewModels/BaseViewModel.cs`
- `StarterApp/ViewModels/ItemListViewModel.cs` — `[RelayCommand]`, `[ObservableProperty]`
- `StarterApp/Services/IUserNotificationService.cs` and `UserNotificationService.cs`
- `StarterApp/Services/INavigationService.cs` and `NavigationService.cs`
- Any `Views/*.xaml.cs` — should be near-empty code-behind

---

## Objective 6 — Repository Pattern

**What it covers:** A reusable `IRepository<T>` generic interface; concrete repositories for Items, Rentals, and Reviews; repositories registered in dependency injection.

### How to prove it works

#### Things to show in code
1. Open `StarterApp.Database/Data/Repositories/IRepository.cs` — the generic `GetAllAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync` contract.
2. Open `IItemRepository.cs`, `IRentalRepository.cs`, `IReviewRepository.cs` — domain-specific extensions of the base interface.
3. Open `ItemRepository.cs` (or Rental/Review) — concrete EF Core implementation using `AppDbContext`.
4. Open `StarterApp.Api/Program.cs` — show the three `AddScoped` DI registrations:
   ```csharp
   builder.Services.AddScoped<IItemRepository, ItemRepository>();
   builder.Services.AddScoped<IRentalRepository, RentalRepository>();
   builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
   ```

#### Key files to open
- `StarterApp.Database/Data/Repositories/IRepository.cs`
- `StarterApp.Database/Data/Repositories/IItemRepository.cs`
- `StarterApp.Database/Data/Repositories/ItemRepository.cs`
- `StarterApp.Api/Program.cs` — DI registration block

---

## Objective 7 — Database and Migrations

**What it covers:** PostgreSQL database with EF Core schema management; Review entity wired into the domain model.

### How to prove it works

#### Things to show
1. Open `StarterApp.Database/Data/AppDbContext.cs` — show `DbSet<Item>`, `DbSet<Rental>`, `DbSet<Review>`, and their model configurations.
2. Open `StarterApp.Database/Migrations/` — point out migration files generated by EF Core.
3. Open `StarterApp.Database/Models/Review.cs` — show Rating (1–5 constraint), Comment, FK to Item and User.
4. Show the database running:
   ```bash
   docker ps         # confirms postgres container is up
   ```

#### Key files to open
- `StarterApp.Database/Data/AppDbContext.cs`
- `StarterApp.Database/Models/Review.cs`
- `StarterApp.Database/Models/Item.cs` and `User.cs` — navigation properties to `List<Review>`

---

## Objective 8 — Dev Container and Reliability

**What it covers:** The development environment starts automatically, the API is reachable from the Android emulator, and the watchdog recovers dropped services.

### How to prove it works

#### Things to show
1. Open a terminal and run:
   ```bash
   curl http://localhost:8080/health    # or any endpoint
   ```
2. Show `setup/ensure-dev-runtime.sh` — single script that starts the API container, waits for readiness, and sets up ADB port forwarding.
3. Explain that VS Code `.devcontainer` tasks trigger this script on every container attach so the environment self-heals on reload.
4. Run `adb devices` in terminal to confirm emulator connectivity.

#### Key files to open
- `setup/ensure-dev-runtime.sh`
- `docker-compose.yml`
- `.devcontainer/devcontainer.json` (if present)

---

## Quick Evidence Summary Table

| Objective | Key API endpoint / file to show | Status |
|---|---|---|
| JWT Authentication | `POST /auth/token`, `AuthenticationInterceptor.cs` | ✅ Done |
| Item Management | `GET /items`, `POST /items`, `PUT /items/{id}` | ✅ Done |
| Rental Request (create) | `POST /rentals`, `RentalApiService.cs` | ✅ Done |
| Outgoing Requests UI | `RentalListPage.xaml`, `RentalListViewModel.cs` | ✅ Done |
| Incoming Requests UI | Owner section in `RentalListPage.xaml` | ✅ Done |
| Active vs Past split | `LoadAsync` in `RentalListViewModel.cs` | ✅ Done |
| MVVM pattern | `BaseViewModel.cs`, `[RelayCommand]`, `IUserNotificationService` | ✅ Done |
| Repository pattern | `IRepository.cs`, `ItemRepository.cs`, DI in `Program.cs` | ✅ Done |
| Review entity | `Review.cs`, `AppDbContext.cs` | ✅ Done |
| Dev container reliability | `ensure-dev-runtime.sh`, `docker-compose.yml` | ✅ Done |
| Tests / Coverage | xUnit project | ❌ Not yet |
| CI/CD workflow | GitHub Actions `.yml` | ❌ Not yet |
| PostGIS location search | ST_DWithin query | ❌ Not yet |

---

## Presentation Tips

- **Show working API calls first** using a REST client (curl, Postman, or browser DevTools) before launching the app — this proves the backend is independent and correct.
- **Walk through one full vertical slice** (e.g., login → browse items → submit rental request → owner approves → both users see updated status) to demonstrate the end-to-end flow in one narrative.
- **Point to interfaces, not just implementations** when discussing architecture — showing `IRepository<T>` and then `ItemRepository` proves the pattern, not just the code.
- **Be upfront about gaps** — acknowledge tests and CI/CD are not done rather than skipping over them. It shows maturity.
