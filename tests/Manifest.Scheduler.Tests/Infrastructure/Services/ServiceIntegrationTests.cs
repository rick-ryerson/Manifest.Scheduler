using Manifest.Scheduler.Domain.Common;
using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Infrastructure.Persistence;
using Manifest.Scheduler.Infrastructure.Repositories;
using Manifest.Scheduler.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Manifest.Scheduler.Tests.Infrastructure.Services;

/// <summary>
/// Integration tests for PersonService and OrganizationService.
///
/// Each test uses an isolated SQLite in-memory database (shared via an open
/// SqliteConnection) so raw SQL executed by ExecuteSqlAsync works correctly
/// and all EF Core operations are visible across context instances.
///
/// Why SQLite instead of InMemory:
///   AssignPersonToPartyAsync / AssignOrganizationToPartyAsync use
///   ExecuteSqlAsync to insert only into the subtype table. The EF Core
///   InMemory provider does not support raw SQL; SQLite does.
/// </summary>
public class ServiceIntegrationTests : IDisposable
{
    // ── Per-test SQLite connection (kept open so the in-memory DB survives) ──
    private readonly SqliteConnection _connection;

    public ServiceIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Create the schema once on this connection; all subsequent contexts
        // that share it will see the same tables.
        using var schemaCtx = BuildContext(Guid.Empty);
        schemaCtx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    // ── Factory helpers ──────────────────────────────────────────────────────

    /// <summary>Builds an ApplicationDbContext scoped to the given tenant.</summary>
    private ApplicationDbContext BuildContext(Guid? tenantId)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(s => s.CurrentTenantId).Returns(tenantId);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .EnableServiceProviderCaching(false)
            .Options;

        return new ApplicationDbContext(options, tenantMock.Object);
    }

    /// <summary>
    /// Builds a mock ICurrentTenantService for a given tenantId.
    /// </summary>
    private static ICurrentTenantService BuildTenantService(Guid tenantId)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.Setup(s => s.CurrentTenantId).Returns(tenantId);
        return mock.Object;
    }

    /// <summary>
    /// Builds a PersonService and its dependencies, all sharing the test connection
    /// and scoped to <paramref name="tenantId"/>.
    /// </summary>
    private (PersonService service, ApplicationDbContext context) BuildPersonService(Guid tenantId)
    {
        var ctx = BuildContext(tenantId);
        var personRepo = new PersonRepository(ctx);
        var partyRepo = new PartyRepository(ctx);
        var tenantService = BuildTenantService(tenantId);
        var service = new PersonService(personRepo, partyRepo, ctx, tenantService);
        return (service, ctx);
    }

    /// <summary>
    /// Builds an OrganizationService and its dependencies, scoped to <paramref name="tenantId"/>.
    /// </summary>
    private (OrganizationService service, ApplicationDbContext context) BuildOrganizationService(Guid tenantId)
    {
        var ctx = BuildContext(tenantId);
        var orgRepo = new OrganizationRepository(ctx);
        var partyRepo = new PartyRepository(ctx);
        var tenantService = BuildTenantService(tenantId);
        var service = new OrganizationService(orgRepo, partyRepo, ctx, tenantService);
        return (service, ctx);
    }

    // ────────────────────────────────────────────────────────────────────────
    // PersonService — CreatePersonAsync
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// EF Core TPT verification: a single CreatePersonAsync call must produce
    /// one row in Parties (the base table) AND one row in People (the subtype
    /// table), both sharing the same primary key.
    /// </summary>
    [Fact]
    public async Task CreatePersonAsync_InsertsRowsInBothPartiesAndPeopleTables()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (service, _) = BuildPersonService(tenantId);

        var person = new Person { FirstName = "Leia", LastName = "Organa" };

        // Act
        var created = await service.CreatePersonAsync(person);

        // Assert — open a raw verification context (bypasses tenant filter)
        using var verifyCtx = BuildContext(tenantId);

        var partyRow = await verifyCtx.Parties
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == created.Id);

        var personRow = await verifyCtx.People
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == created.Id);

        Assert.NotNull(partyRow);   // TPT base row exists
        Assert.NotNull(personRow);  // TPT subtype row exists
        Assert.Equal(created.Id, partyRow.Id);
        Assert.Equal(created.Id, personRow.Id);
        Assert.Equal("Leia", personRow.FirstName);
        Assert.Equal("Organa", personRow.LastName);
    }

    /// <summary>
    /// The service must overwrite any caller-supplied TenantId with the resolved
    /// tenant from ICurrentTenantService so callers cannot stamp records with a
    /// foreign tenant's ID.
    /// </summary>
    [Fact]
    public async Task CreatePersonAsync_OverwritesTenantIdFromCurrentTenantService()
    {
        // Arrange
        var realTenantId = Guid.NewGuid();
        var spoofedTenantId = Guid.NewGuid(); // caller tries to set a different tenant
        var (service, _) = BuildPersonService(realTenantId);

        var person = new Person
        {
            TenantId = spoofedTenantId, // should be ignored
            FirstName = "Han",
            LastName = "Solo"
        };

        // Act
        var created = await service.CreatePersonAsync(person);

        // Assert — TenantId must reflect the service's resolved tenant, not the caller's value
        Assert.Equal(realTenantId, created.TenantId);

        using var verifyCtx = BuildContext(realTenantId);
        var persisted = await verifyCtx.People.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == created.Id);
        Assert.NotNull(persisted);
        Assert.Equal(realTenantId, persisted.TenantId);
    }

    /// <summary>
    /// TenantId set by the service must be persisted and readable through the
    /// tenant-scoped query filter (i.e. the record is visible to the correct tenant).
    /// </summary>
    [Fact]
    public async Task CreatePersonAsync_PersistsTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (service, _) = BuildPersonService(tenantId);

        // Act
        var created = await service.CreatePersonAsync(new Person { FirstName = "Han", LastName = "Solo" });

        // Assert — read back through the same tenant-scoped context
        using var verifyCtx = BuildContext(tenantId);
        var persisted = await verifyCtx.People.FindAsync(created.Id);

        Assert.NotNull(persisted);
        Assert.Equal(tenantId, persisted.TenantId);
    }

    // ────────────────────────────────────────────────────────────────────────
    // PersonService — AssignPersonToPartyAsync
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// AssignPersonToPartyAsync must insert a People row for an existing Party
    /// without touching the Parties table (raw SQL subtype-only insert).
    /// The inserted Person shares the same Id as the pre-existing Party.
    /// </summary>
    [Fact]
    public async Task AssignPersonToPartyAsync_InsertsPersonRowLinkedToExistingParty()
    {
        // Arrange — create an Organization first so a Parties row already exists.
        var tenantId = Guid.NewGuid();
        var (orgService, _) = BuildOrganizationService(tenantId);

        var org = await orgService.CreateOrganizationAsync(new Organization { Name = "Rebel Alliance" });
        var existingPartyId = org.Id;

        // Act — assign a Person record to that same Party identity
        var (personService, _) = BuildPersonService(tenantId);
        var assigned = await personService.AssignPersonToPartyAsync(
            existingPartyId,
            new Person { FirstName = "Mon", LastName = "Mothma" });

        // Assert
        using var verifyCtx = BuildContext(tenantId);
        var personRow = await verifyCtx.People
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == existingPartyId);

        Assert.NotNull(personRow);
        Assert.Equal(existingPartyId, assigned.Id);
        Assert.Equal(tenantId, assigned.TenantId);
        Assert.Equal("Mon", personRow.FirstName);
        Assert.Equal("Mothma", personRow.LastName);
    }

    /// <summary>
    /// AssignPersonToPartyAsync must throw when the target Party does not exist
    /// in the current tenant's scope.
    /// </summary>
    [Fact]
    public async Task AssignPersonToPartyAsync_ThrowsWhenPartyDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (service, _) = BuildPersonService(tenantId);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignPersonToPartyAsync(Guid.NewGuid(), new Person
            {
                FirstName = "Ghost",
                LastName = "Nobody"
            }));
    }

    /// <summary>
    /// A Party that belongs to Tenant B must not be assignable from a service
    /// scoped to Tenant A — the tenant-filtered ExistsAsync check must prevent it.
    /// </summary>
    [Fact]
    public async Task AssignPersonToPartyAsync_ThrowsWhenPartyBelongsToADifferentTenant()
    {
        // Arrange — create a Party under Tenant B
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (orgServiceB, _) = BuildOrganizationService(tenantB);
        var orgB = await orgServiceB.CreateOrganizationAsync(new Organization { Name = "Empire" });

        // Act — try to assign a Person to Tenant B's Party from a Tenant A service
        var (personServiceA, _) = BuildPersonService(tenantA);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => personServiceA.AssignPersonToPartyAsync(
                orgB.Id, new Person { FirstName = "Darth", LastName = "Vader" }));
    }

    // ────────────────────────────────────────────────────────────────────────
    // Tenant isolation
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Data created under Tenant A must not be visible to a service instance
    /// scoped to Tenant B, and vice-versa. The global query filter on
    /// ApplicationDbContext must enforce this boundary automatically.
    /// </summary>
    [Fact]
    public async Task PersonService_TenantIsolation_CannotSeeOtherTenantData()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (serviceA, _) = BuildPersonService(tenantA);
        var (serviceB, _) = BuildPersonService(tenantB);

        await serviceA.CreatePersonAsync(new Person { FirstName = "Luke", LastName = "Skywalker" });
        await serviceB.CreatePersonAsync(new Person { FirstName = "Darth", LastName = "Vader" });

        // Act — each context is tenant-filtered
        using var ctxA = BuildContext(tenantA);
        using var ctxB = BuildContext(tenantB);

        var peopleSeenByA = await ctxA.People.ToListAsync();
        var peopleSeenByB = await ctxB.People.ToListAsync();

        // Assert — each tenant sees only its own record
        Assert.Single(peopleSeenByA);
        Assert.Equal(tenantA, peopleSeenByA[0].TenantId);
        Assert.Equal("Luke", peopleSeenByA[0].FirstName);

        Assert.Single(peopleSeenByB);
        Assert.Equal(tenantB, peopleSeenByB[0].TenantId);
        Assert.Equal("Darth", peopleSeenByB[0].FirstName);

        // Both rows exist in total (filter is not deleting data)
        using var rawCtx = BuildContext(Guid.Empty);
        var allPeople = await rawCtx.People.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, allPeople.Count);
    }

    // ────────────────────────────────────────────────────────────────────────
    // OrganizationService — CreateOrganizationAsync
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// CreateOrganizationAsync must insert into both Parties and Organizations
    /// tables (TPT), stamp the correct TenantId, and the record must be readable
    /// through the tenant-scoped filter.
    /// </summary>
    [Fact]
    public async Task CreateOrganizationAsync_InsertsRowsInBothPartiesAndOrganizationsTables()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (service, _) = BuildOrganizationService(tenantId);

        // Act
        var created = await service.CreateOrganizationAsync(new Organization { Name = "Galactic Senate" });

        // Assert
        using var verifyCtx = BuildContext(tenantId);

        var partyRow = await verifyCtx.Parties
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == created.Id);

        var orgRow = await verifyCtx.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == created.Id);

        Assert.NotNull(partyRow);
        Assert.NotNull(orgRow);
        Assert.Equal(created.Id, partyRow.Id);
        Assert.Equal("Galactic Senate", orgRow.Name);
        Assert.Equal(tenantId, partyRow.TenantId);
    }
}
