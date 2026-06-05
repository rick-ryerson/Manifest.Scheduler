using Manifest.Scheduler.Domain.Common;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Manifest.Scheduler.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentTenantService _currentTenantService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentTenantService currentTenantService)
        : base(options)
    {
        _currentTenantService = currentTenantService;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply a global query filter for all entities that implement IMustHaveTenant,
        // automatically scoping every query to the current tenant.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProperty = Expression.Property(parameter, nameof(IMustHaveTenant.TenantId));

            // _currentTenantService.CurrentTenantId ?? Guid.Empty
            var currentTenantServiceExpr = Expression.Constant(_currentTenantService);
            var currentTenantIdExpr = Expression.Property(currentTenantServiceExpr,
                nameof(ICurrentTenantService.CurrentTenantId));
            var emptyGuid = Expression.Constant(Guid.Empty);
            var coalesce = Expression.Coalesce(currentTenantIdExpr, emptyGuid);

            var filter = Expression.Lambda(
                Expression.Equal(tenantIdProperty, coalesce),
                parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}
