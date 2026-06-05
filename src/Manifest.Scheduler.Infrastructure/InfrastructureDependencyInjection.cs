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
            options.UseSqlServer(
                config.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        // ── Tenant resolution ──────────────────────────────────────────────────
        // IHttpContextAccessor is required by CurrentTenantService to read
        // the X-Tenant-Id request header.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();

        // ── Repositories ───────────────────────────────────────────────────────
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();

        return services;
    }
}
