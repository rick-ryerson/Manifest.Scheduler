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

# Run the API
dotnet run --project src/Manifest.Scheduler.Api
```

## Architecture

This is a .NET 8 solution with Clean Architecture layering:

- **Domain** (`src/Manifest.Scheduler.Domain`) — entities, value objects, domain interfaces. No dependencies on other projects.
- **Infrastructure** (`src/Manifest.Scheduler.Infrastructure`) — implements domain interfaces; handles persistence, external services, etc. References Domain.
- **Api** (`src/Manifest.Scheduler.Api`) — ASP.NET Core Web API with Swagger/OpenAPI. References Infrastructure (and transitively Domain).
- **Tests** (`tests/Manifest.Scheduler.Tests`) — xUnit test project. References all three source projects.

Dependency flow: `Api → Infrastructure → Domain`
