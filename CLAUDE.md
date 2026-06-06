# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run a single test class or method
dotnet test --filter "FullyQualifiedName~ClassName"
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"

# Run the API locally (requires a local PostgreSQL instance on port 5432)
dotnet run --project src/Manifest.Scheduler.Api
```

## Docker

The application is containerised with a multi-stage `Dockerfile` and orchestrated via `docker-compose.yml`.

```bash
# Build images and start all services (API + PostgreSQL) in the foreground
docker-compose up --build

# Run in the background
docker-compose up --build -d

# Tail logs when running detached
docker-compose logs -f api

# Stop and remove containers (data volume is preserved)
docker-compose down

# Stop and remove containers AND the data volume (full reset)
docker-compose down -v
```

### Services

| Service | Container port | Host port | Notes |
|---------|---------------|-----------|-------|
| `api`   | 8080          | 8080      | Swagger UI at `http://localhost:8080/swagger` |
| `db`    | 5432          | 5432      | PostgreSQL 16 (user: `scheduler`, db: `ManifestScheduler`) |

On first start the API automatically applies any pending EF Core migrations via `Database.Migrate()` in `Program.cs`. The API waits for the database to pass its health-check before starting (configured via `depends_on: condition: service_healthy`).

### Environment variables

The `docker-compose.yml` overrides the connection string for the `api` service using the environment variable convention:

```
ConnectionStrings__DefaultConnection=Host=db;Port=5432;Database=ManifestScheduler;Username=scheduler;Password=scheduler_pass
```

To supply secrets outside of Compose, set the variable in your shell or a `.env` file before running `docker-compose up`.

## Architecture

This is a .NET 8 solution with Clean Architecture layering:

- **Domain** (`src/Manifest.Scheduler.Domain`) — entities, value objects, domain interfaces. No dependencies on other projects.
- **Infrastructure** (`src/Manifest.Scheduler.Infrastructure`) — implements domain interfaces; handles persistence, external services, etc. References Domain.
- **Api** (`src/Manifest.Scheduler.Api`) — ASP.NET Core Web API with Swagger/OpenAPI. References Infrastructure (and transitively Domain).
- **Tests** (`tests/Manifest.Scheduler.Tests`) — xUnit test project. References all three source projects.

Dependency flow: `Api → Infrastructure → Domain`
