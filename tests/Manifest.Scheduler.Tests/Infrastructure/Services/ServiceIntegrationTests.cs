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
    /// Builds a PersonService and its dependencies, all sharing the test connection
    /// and scoped to <paramref name="tenantId"/>.
    /// </summary>
    private (PersonService service, ApplicationDbContext context) BuildPersonService(Guid tenantId)
    {
        var ctx = BuildContext(tenantId);
        var personRepo = new PersonRepository(ctx);
        var partyRepo = new PartyRepository(ctx);
        var service = new PersonService(personRepo, partyRepo, ctx);
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
        var service = new OrganizationService(orgRepo, partyRepo, ctx);
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

        var person = new Person
        {
            TenantId = tenantId,
            FirstName = "Leia",
            LastName = "Organa"
        };

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
    /// TenantId set on the Person entity must be persisted to the database
    /// and readable back through the tenant-scoped query filter.
    /// </summary>
    [Fact]
    public async Task CreatePersonAsync_PersistsTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (service, _) = BuildPersonService(tenantId);

        var person = new Person
        {
            TenantId = tenantId,
            FirstName = "Han",
            LastName = "Solo"
        };

        // Act
        var created = await service.CreatePersonAsync(person);

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

        var org = await orgService.CreateOrganizationAsync(new Organization
        {
            TenantId = tenantId,
            Name = "Rebel Alliance"
        });

        var existingPartyId = org.Id;

        // Act — assign a Person record to that same Party identity
        var (personService, _) = BuildPersonService(tenantId);

        var person = new Person { FirstName = "Mon", LastName = "Mothma" };
        var assigned = await personService.AssignPersonToPartyAsync(existingPartyId, person);

        // Assert
        using var verifyCtx = BuildContext(tenantId);

        var personRow = await verifyCtx.People
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == existingPartyId);

        Assert.NotNull(personRow);
        Assert.Equal(existingPartyId, assigned.Id);
        Assert.Equal("Mon", personRow.FirstName);
        Assert.Equal("Mothma", personRow.LastName);
    }

    /// <summary>
    /// AssignPersonToPartyAsync must throw when the target Party does not exist,
    /// leaving the database unchanged.
    /// </summary>
    [Fact]
    public async Task AssignPersonToPartyAsync_ThrowsWhenPartyDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (service, _) = BuildPersonService(tenantId);

        var ghostPartyId = Guid.NewGuid(); // never persisted

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignPersonToPartyAsync(ghostPartyId, new Person
            {
                FirstName = "Ghost",
                LastName = "Nobody"
            }));
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

        // Seed one Person per tenant
        await serviceA.CreatePersonAsync(new Person
        {
            TenantId = tenantA,
            FirstName = "Luke",
            LastName = "Skywalker"
        });

        await serviceB.CreatePersonAsync(new Person
        {
            TenantId = tenantB,
            FirstName = "Darth",
            LastName = "Vader"
        });

        // Act — each service reads its own context (tenant-filtered)
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
    /// tables (TPT), and the TenantId must be persisted correctly.
    /// </summary>
    [Fact]
    public async Task CreateOrganizationAsync_InsertsRowsInBothPartiesAndOrganizationsTables()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var (service, _) = BuildOrganizationService(tenantId);

        var org = new Organization
        {
            TenantId = tenantId,
            Name = "Galactic Senate"
        };

        // Act
        var created = await service.CreateOrganizationAsync(org);

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
