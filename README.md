# Issues

```
  StarterApp net9.0-android failed with 1 error(s) (1.0s)
    Resources/values/colors.xml : error APT2126: 
      file not found.
```

```
root ➜ /workspace (main) $ dotnet build
Restore complete (0.8s)
  StarterApp.Database succeeded (0.2s) → StarterApp.Database/bin/Debug/net9.0/StarterApp.Database.dll
  StarterApp.Migrations succeeded (0.2s) → StarterApp.Migrations/bin/Debug/net9.0/StarterApp.Migrations.dll
  StarterApp net9.0-android failed with 1 error(s) (0.8s)
    Resources/values/colors.xml : error APT2126: 
      file not found.
```
---

---
title: "StarterApp readme"
parent: StarterApp
grand_parent: C# practice
nav_order: 5
mermaid: true
---

# StarterApp

The purpose of this app is to act as a starting point for further development. It provides some
basic features including:

* Database integration and migrations
* Role-based security
* Local authentication
* Example navigation

This version of the app uses PostgreSQL for data storage and Entity Framework Core for object-relational mapping
and migrations.

The app now uses JWT-based authentication through a backend API (`StarterApp.Api`) instead of local-only login.

To fully understand how it works, you should follow an appropriate set of tutorials such as 
[this one](https://edinburgh-napier.github.io/SET09102/tutorials/csharp/) which covers all of the main
concepts and techniques used here. However, if you want to jump straight in and work out any problems
as you go along, that will also work. The code uses structured comments for use with the 
[Doxygen](https://www.doxygen.nl/) documentation generator tool. 

You can use any development environment with this project including

* [Rider](https://www.jetbrains.com/rider/)
* [Visual Studio](https://visualstudio.microsoft.com/)
* [Visual Studio Code](https://code.visualstudio.com/)

The instructions assume you will be using VSCode since that is a lowest-common-denominator choice.

## Compatibility

This app is built using the following tool versions.

| Name                                                                                      | Version     |
|-------------------------------------------------------------------------------------------|-------------|
| [.NET](https://dotnet.microsoft.com/en-us/)                                               | 8.0 / 9.0   |
| [PostgreSQL Docker image](https://hub.docker.com/_/postgres)                              | 16          |


## Getting started

### JWT API Architecture

Authentication now flows through HTTP endpoints:

* `POST /auth/token` issues access + refresh tokens
* `POST /auth/refresh` rotates refresh token and issues a new access token
* `POST /auth/register` creates new user accounts
* `POST /auth/change-password` requires a valid Bearer token
* `GET /auth/me` requires a valid Bearer token

The MAUI client stores tokens securely and refreshes access tokens automatically when they expire.

### Prerequisites

Before using this app, ensure you have:

1. **.NET SDK 8.0** or later installed
2. **Docker** installed and running
3. **PostgreSQL container** running (see [dev-environment tutorial](https://edinburgh-napier.github.io/SET09102/tutorials/csharp/dev-environment/))

### Configuration

1. Copy `StarterApp.Database/appsettings.json.template` to `StarterApp.Database/appsettings.json`
2. Update the connection string with your PostgreSQL credentials:
   ```json
   {
     "ConnectionStrings": {
       "DevelopmentConnection": "Host=localhost;Username=student_user;Password=password123;Database=starterapp"
     }
   }
   ```

### Initial Setup

1. Navigate to the Migrations project and create the initial migration:
   ```bash
   cd StarterApp.Migrations
   dotnet ef migrations add InitialCreate
   ```

2. Apply the migration to create the database:
   ```bash
   dotnet ef database update
   ```

3. Build and run the application:
   ```bash
   cd ../StarterApp
   dotnet build
   dotnet run
   ```

### Run Database + API with Docker

From the repository root:

```bash
docker compose up -d db api
```

API will be available on `http://localhost:8080`.

Swagger UI:

`http://localhost:8080/swagger`

If you are running Android emulator builds, the MAUI app default base URL is already set to `http://10.0.2.2:8080`.

For other environments, set:

```bash
AUTH_API_BASE_URL=http://<your-host>:8080
```

You should also set a secure JWT signing key in container or environment configuration:

```bash
Jwt__SigningKey=<long-random-secret-at-least-32-chars>
```

### Tutorial

For a comprehensive guide on using this app and understanding its architecture, see the
[MAUI + MVVM + Database Tutorial](https://edinburgh-napier.github.io/SET09102/tutorials/csharp/maui-mvvm-database/).
