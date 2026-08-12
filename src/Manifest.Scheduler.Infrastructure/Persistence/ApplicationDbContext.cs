using Manifest.Scheduler.Domain.Common;
using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

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

    public DbSet<Party> Parties => Set<Party>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<PartyRole> PartyRoles => Set<PartyRole>();

    // Referenced by the query filter below via `this.CurrentTenantId` rather than capturing
    // _currentTenantService directly. EF Core caches the compiled model (including query
    // filter expressions) once per DbContextOptions for the app's lifetime; a filter that
    // captures an injected service as an Expression.Constant freezes that specific instance
    // forever, so every request after the first would see the first request's tenant. A
    // `this`-scoped member access is special-cased by EF Core to re-resolve against whichever
    // DbContext instance is actually executing the query, so this stays correct per-request
    // while keeping model caching enabled.
    private Guid CurrentTenantId => _currentTenantService.CurrentTenantId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── TPT table mapping for the Party hierarchy ──────────────────────────
        // Each concrete type gets its own table; EF Core links them via a shared PK.
        // PersonService / OrganizationService rely on these table names when they
        // use ExecuteSqlAsync to insert only into a subtype table for an existing Party.
        modelBuilder.Entity<Party>().ToTable("Parties");
        modelBuilder.Entity<Person>().ToTable("People");
        modelBuilder.Entity<Organization>().ToTable("Organizations");

        // Apply a global query filter for all entities that implement IMustHaveTenant,
        // automatically scoping every query to the current tenant.
        // Only the root of each hierarchy gets the filter; EF Core propagates it to
        // derived types automatically. Setting it on derived types too causes:
        //   "A filter may only be applied to the root entity type."
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
                continue;

            // Skip derived types whose immediate base also implements IMustHaveTenant
            // (e.g. Person : Party, Organization : Party — Party carries the filter).
            if (entityType.BaseType != null &&
                typeof(IMustHaveTenant).IsAssignableFrom(entityType.BaseType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProperty = Expression.Property(parameter, nameof(IMustHaveTenant.TenantId));

            // this.CurrentTenantId — a `this`-scoped member access, not a captured service
            // instance. See the CurrentTenantId property above for why this matters.
            // The constant's type must be pinned to ApplicationDbContext explicitly: a bare
            // Expression.Constant(this) infers its type from the runtime type (e.g. a test
            // subclass like TestableDbContext), and CurrentTenantId — being private — can't be
            // resolved by name against a derived type via reflection.
            var dbContextExpr = Expression.Constant(this, typeof(ApplicationDbContext));
            var currentTenantIdPropertyInfo = typeof(ApplicationDbContext).GetProperty(
                nameof(CurrentTenantId), BindingFlags.NonPublic | BindingFlags.Instance)!;
            var currentTenantIdExpr = Expression.Property(dbContextExpr, currentTenantIdPropertyInfo);

            var filter = Expression.Lambda(
                Expression.Equal(tenantIdProperty, currentTenantIdExpr),
                parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}
