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
}
