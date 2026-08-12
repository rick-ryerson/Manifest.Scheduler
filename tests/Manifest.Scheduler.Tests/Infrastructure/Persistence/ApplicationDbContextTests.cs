using Manifest.Scheduler.Domain.Common;
using Manifest.Scheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace Manifest.Scheduler.Tests.Infrastructure.Persistence;

/// <summary>
/// Extends ApplicationDbContext to register TenantTestEntity without
/// adding a verification-only entity to the production context.
/// </summary>
internal class TestableDbContext : ApplicationDbContext
{
    public TestableDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentTenantService currentTenantService)
        : base(options, currentTenantService) { }

    public DbSet<TenantTestEntity> TenantTestEntities => Set<TenantTestEntity>();
}

public class ApplicationDbContextTests
{
    /// <summary>
    /// Builds a TestableDbContext scoped to the given tenant, sharing the
    /// provided InMemoryDatabaseRoot so seed and query contexts see the same data.
    ///
    /// EnableServiceProviderCaching(false) is required so each context instance
    /// rebuilds OnModelCreating with its own ICurrentTenantService mock, preventing
    /// EF Core from freezing the first mock's value into the shared compiled model.
    /// </summary>
    private static TestableDbContext BuildContext(Guid? tenantId, InMemoryDatabaseRoot dbRoot)
    {
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(s => s.CurrentTenantId).Returns(tenantId);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("TenantFilterVerification", dbRoot)
            .EnableServiceProviderCaching(false)
            .Options;

        return new TestableDbContext(options, tenantService.Object);
    }

    [Fact]
    public void GlobalQueryFilter_ReturnsOnlyEntitiesForCurrentTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbRoot = new InMemoryDatabaseRoot();

        using (var seedCtx = BuildContext(tenantA, dbRoot))
        {
            seedCtx.TenantTestEntities.AddRange(
                new TenantTestEntity { Id = 1, Name = "Tenant A entity", TenantId = tenantA },
                new TenantTestEntity { Id = 2, Name = "Tenant B entity", TenantId = tenantB }
            );
            seedCtx.SaveChanges();

            // Sanity-check: both rows were persisted
            Assert.Equal(2, seedCtx.TenantTestEntities.IgnoreQueryFilters().Count());
        }

        // Act — open a context scoped to tenantA
        using var ctx = BuildContext(tenantA, dbRoot);
        var results = ctx.TenantTestEntities.ToList();

        // Assert — only the tenantA row is visible through the filter
        Assert.Single(results);
        Assert.Equal(tenantA, results[0].TenantId);
        Assert.Equal("Tenant A entity", results[0].Name);
    }

    /// <summary>
    /// Regression test for a real cross-tenant data leak: OnModelCreating originally
    /// captured the injected ICurrentTenantService instance itself as an
    /// Expression.Constant inside the query filter. With EF Core's model caching
    /// enabled — the production default; InfrastructureDependencyInjection.AddInfrastructure
    /// never disables it — the compiled model (and the frozen service reference baked
    /// into it) is built once from whichever DbContext instance runs first, then reused
    /// for every later instance sharing the same DbContextOptions. In the running app
    /// this meant every request after the very first request silently saw that first
    /// request's tenant data, regardless of its own X-Tenant-Id header.
    ///
    /// Unlike GlobalQueryFilter_ReturnsOnlyEntitiesForCurrentTenant above, this test
    /// does NOT call EnableServiceProviderCaching(false) and shares a single
    /// DbContextOptions instance across two ICurrentTenantService mocks — reproducing
    /// the exact conditions that hid this bug until it surfaced in a running container.
    /// The fix makes the filter reference a `this`-scoped property on ApplicationDbContext
    /// (CurrentTenantId) instead, which EF Core re-resolves against the actual executing
    /// instance rather than freezing at model-build time.
    /// </summary>
    [Fact]
    public void GlobalQueryFilter_ReEvaluatesPerInstance_WhenModelIsCachedAcrossInstances()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbRoot = new InMemoryDatabaseRoot();

        // Provider caching left at its default (enabled) — this is what production uses.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("TenantFilterCachingRegression", dbRoot)
            .Options;

        var tenantAService = new Mock<ICurrentTenantService>();
        tenantAService.Setup(s => s.CurrentTenantId).Returns(tenantA);

        using (var seedCtx = new TestableDbContext(options, tenantAService.Object))
        {
            seedCtx.TenantTestEntities.AddRange(
                new TenantTestEntity { Id = 1, Name = "Tenant A entity", TenantId = tenantA },
                new TenantTestEntity { Id = 2, Name = "Tenant B entity", TenantId = tenantB }
            );
            seedCtx.SaveChanges();

            // This first context is the one that compiles (and caches) the model.
            Assert.Single(seedCtx.TenantTestEntities.ToList());
        }

        // Act — a second, independent context instance sharing the same cached model,
        // scoped to a DIFFERENT tenant than the one that built it.
        var tenantBService = new Mock<ICurrentTenantService>();
        tenantBService.Setup(s => s.CurrentTenantId).Returns(tenantB);
        using var tenantBCtx = new TestableDbContext(options, tenantBService.Object);

        var results = tenantBCtx.TenantTestEntities.ToList();

        // Assert — tenantB's own context must see only its own row, not the row
        // belonging to tenantA (the tenant that happened to build the model first).
        Assert.Single(results);
        Assert.Equal(tenantB, results[0].TenantId);
        Assert.Equal("Tenant B entity", results[0].Name);
    }
}
