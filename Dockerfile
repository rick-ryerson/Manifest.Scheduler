# ── Stage 1: Build ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first so NuGet restore is cached separately
# from source code changes.
COPY Manifest.Scheduler.sln ./
COPY src/Manifest.Scheduler.Domain/Manifest.Scheduler.Domain.csproj          src/Manifest.Scheduler.Domain/
COPY src/Manifest.Scheduler.Infrastructure/Manifest.Scheduler.Infrastructure.csproj  src/Manifest.Scheduler.Infrastructure/
COPY src/Manifest.Scheduler.Api/Manifest.Scheduler.Api.csproj                src/Manifest.Scheduler.Api/

RUN dotnet restore

# Copy the remaining source (tests excluded via .dockerignore)
COPY src/ src/

# Publish a self-contained, trimmed release build of the API
RUN dotnet publish src/Manifest.Scheduler.Api/Manifest.Scheduler.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ── Stage 2: Runtime ──────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for defence-in-depth
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish ./

# Kestrel will listen on port 8080 inside the container
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Manifest.Scheduler.Api.dll"]
