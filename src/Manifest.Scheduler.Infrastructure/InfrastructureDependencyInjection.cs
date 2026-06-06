using Manifest.Scheduler.Domain.Common;
using Manifest.Scheduler.Domain.GalacticSenate.Repositories;
using Manifest.Scheduler.Infrastructure.Persistence;
using Manifest.Scheduler.Infrastructure.Repositories;
using Manifest.Scheduler.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Manifest.Scheduler.Infrastructure;

public static class InfrastructureDependencyInjection
{
    /// <summary>
    /// Registers all Infrastructure-layer services: EF Core, tenant resolution,
    /// and repositories. Call from Program.cs via builder.Services.AddInfrastructure(...).
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        // ── Persistence ────────────────────────────────────────────────────────
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                config.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        // ── Tenant resolution ──────────────────────────────────────────────────
        // IHttpContextAccessor is required by CurrentTenantService to read
        // the X-Tenant-Id request header.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();

        // ── Repositories ───────────────────────────────────────────────────────
        // IPartyRepository — cross-hierarchy identity queries (existence checks, base record lookup)
        services.AddScoped<IPartyRepository, PartyRepository>();
        // Subtype repositories — creation and management of typed party records
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IPartyRoleRepository, PartyRoleRepository>();

        // ── Services ───────────────────────────────────────────────────────────
        services.AddScoped<IPersonService, PersonService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IPartyRoleService, PartyRoleService>();

        return services;
    }
}
