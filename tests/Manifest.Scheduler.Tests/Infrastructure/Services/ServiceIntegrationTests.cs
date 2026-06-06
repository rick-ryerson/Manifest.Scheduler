using Manifest.Scheduler.Domain.Common;
using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Infrastructure.Persistence;
using Manifest.Scheduler.Infrastructure.Repositories;
using Manifest.Scheduler.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace Manifest.Scheduler.Tests.Infrastructure.Services;

// ── Shared container fixture ──────────────────────────────────────────────────
// IAsyncLifetime on a CollectionFixture means the container starts once before
// the first test in the collection and stops after the last one finishes.
// This keeps total test time low — a typical postgres:16-alpine container boots
// in under 5 seconds.

/// <summary>
/// Starts a PostgreSQL Testcontainer once for all service integration tests,
/// creates the EF Core schema via EnsureCreated, and provides factory helpers
/// that build correctly-wired service and context instances against that database.
/// When Docker is not available the fixture sets <see cref="IsAvailable"/> to
/// <c>false</c> and every test skips cleanly rather than failing.
/// </summary>
public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    // Testcontainers 4.x: pass the image to the constructor; the PostgreSqlContainer
    // has a built-in readiness check (pg_isready) so no custom wait strategy is needed.
    private PostgreSqlContainer? _container;

    /// <summary><c>true</c> when Docker is running and the container started successfully.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Reason to surface in a skipped test when Docker is unavailable.</summary>
    public string SkipReason { get; private set; } = string.Empty;

    /// <summary>Full connection string to the running container database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();

            // Bootstrap the schema once. Any non-empty tenant GUID works here since
            // EnsureCreated does not depend on the tenant context.
            await using var ctx = BuildContext(Guid.NewGuid());
            await ctx.Database.EnsureCreatedAsync();

            IsAvailable = true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            SkipReason = $"Docker is not available — skipping Testcontainers tests. " +
                         $"Start Docker Desktop and re-run to execute these tests. " +
                         $"Details: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    // ── Factories (used by every test) ────────────────────────────────────────

    /// <summary>
    /// Opens an <see cref="ApplicationDbContext"/> scoped to <paramref name="tenantId"/>
    /// against the shared PostgreSQL database.
    ///
    /// <para>
    /// <c>EnableServiceProviderCaching(false)</c> is required so that EF Core
    /// re-evaluates <c>OnModelCreating</c> for each new context instance.
    /// Without it the compiled model — including the tenant filter expression —
    /// is cached for the first context and frozen for all subsequent ones,
    /// meaning every context would filter on the first test's tenant ID.
    /// </para>
    /// </summary>
    public ApplicationDbContext BuildContext(Guid? tenantId)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(s => s.CurrentTenantId).Returns(tenantId);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .EnableServiceProviderCaching(false)
            .Options;

        return new ApplicationDbContext(options, tenantMock.Object);
    }

    /// <summary>Builds a fully-wired <see cref="PersonService"/> for <paramref name="tenantId"/>.</summary>
    public (PersonService service, ApplicationDbContext context) BuildPersonService(Guid tenantId)
    {
        var tenantService = BuildTenantService(tenantId);
        var ctx = BuildContext(tenantId);
        var service = new PersonService(
            new PersonRepository(ctx),
            new PartyRepository(ctx),
            ctx,
            tenantService);
        return (service, ctx);
    }

    /// <summary>Builds a fully-wired <see cref="OrganizationService"/> for <paramref name="tenantId"/>.</summary>
    public (OrganizationService service, ApplicationDbContext context) BuildOrganizationService(Guid tenantId)
    {
        var tenantService = BuildTenantService(tenantId);
        var ctx = BuildContext(tenantId);
        var service = new OrganizationService(
            new OrganizationRepository(ctx),
            new PartyRepository(ctx),
            ctx,
            tenantService);
        return (service, ctx);
    }

    private static ICurrentTenantService BuildTenantService(Guid tenantId)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.Setup(s => s.CurrentTenantId).Returns(tenantId);
        return mock.Object;
    }
}

/// <summary>
/// xUnit collection definition — ensures all tests in <see cref="ServiceIntegrationTests"/>
/// share one <see cref="PostgreSqlContainerFixture"/> instance (i.e. one container).
/// Tests within a collection run sequentially, so there are no parallel-write conflicts.
/// </summary>
[CollectionDefinition(nameof(PostgreSqlCollection))]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlContainerFixture> { }

// ── Tests ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Integration tests for <see cref="PersonService"/> and <see cref="OrganizationService"/>
/// running against a real PostgreSQL database spun up by Testcontainers.
///
/// <para>
/// <b>Test isolation strategy:</b> each test generates its own pair of
/// <c>Guid.NewGuid()</c> tenant IDs. Because every query is filtered by
/// <c>TenantId</c> via the EF Core global query filter, rows from different
/// tests are invisible to one another — no per-test database teardown is needed.
/// </para>
///
/// <para>
/// <b>Why Testcontainers over SQLite?</b> PostgreSQL has stricter identifier
/// handling (quoted names are case-sensitive; SQL type mapping differs) and
/// runs the same engine as a typical production deployment. Tests against a
/// real engine catch SQL compatibility issues that an in-process provider
/// cannot surface.
/// </para>
///
/// <para>
/// <b>Docker required:</b> these tests are automatically skipped when Docker
/// is not running. Start Docker Desktop and re-run to execute them.
/// </para>
/// </summary>
[Collection(nameof(PostgreSqlCollection))]
public sealed class ServiceIntegrationTests
{
    private readonly PostgreSqlContainerFixture _db;

    public ServiceIntegrationTests(PostgreSqlContainerFixture db) => _db = db;

    // ── PersonService.CreatePersonAsync ───────────────────────────────────────

    /// <summary>
    /// EF Core TPT verification: a single <c>CreatePersonAsync</c> call must write
    /// one row into <c>Parties</c> (the hierarchy root table) AND one row into
    /// <c>People</c> (the subtype table), both sharing the same primary key.
    /// </summary>
    [SkippableFact]
    public async Task CreatePersonAsync_InsertsRowsInBothPartiesAndPeopleTables()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);

        var tenantId = Guid.NewGuid();
        var (service, _) = _db.BuildPersonService(tenantId);

        // Act
        var created = await service.CreatePersonAsync(
            new Person { FirstName = "Leia", LastName = "Organa" });

        // Assert — bypass tenant filter to confirm both TPT rows exist
        await using var verify = _db.BuildContext(tenantId);

        var partyRow = await verify.Parties
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == created.Id);

        var personRow = await verify.People
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == created.Id);

        Assert.NotNull(partyRow);   // base-table row (Parties)
        Assert.NotNull(personRow);  // subtype-table row (People)
        Assert.Equal(created.Id, partyRow.Id);
        Assert.Equal(created.Id, personRow.Id);
        Assert.Equal("Leia", personRow.FirstName);
        Assert.Equal("Organa", personRow.LastName);
    }

    /// <summary>
    /// The service must overwrite whatever <c>TenantId</c> the caller places on
    /// the entity with the value resolved from <see cref="ICurrentTenantService"/>.
    /// A caller must not be able to stamp a record with a foreign tenant's ID.
    /// </summary>
    [SkippableFact]
    public async Task CreatePersonAsync_OverwritesTenantIdFromCurrentTenantService()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);

        var realTenantId = Guid.NewGuid();
        var spoofedTenantId = Guid.NewGuid();
        var (service, _) = _db.BuildPersonService(realTenantId);

        var created = await service.CreatePersonAsync(new Person
        {
            TenantId = spoofedTenantId, // should be ignored by the service
            FirstName = "Han",
            LastName = "Solo"
        });

        // TenantId on the returned entity must match the service's resolved tenant
        Assert.Equal(realTenantId, created.TenantId);

        // The persisted row must carry the correct TenantId
        await using var verify = _db.BuildContext(realTenantId);
        var persisted = await verify.People
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == created.Id);

        Assert.NotNull(persisted);
        Assert.Equal(realTenantId, persisted.TenantId);
    }

    /// <summary>
    /// The persisted <c>TenantId</c> must be readable through the tenant-scoped
    /// query filter — i.e. the record is visible when querying as the correct tenant
    /// and invisible when querying as any other tenant.
    /// </summary>
    [SkippableFact]
    public async Task CreatePersonAsync_TenantIdPersistsAndIsVisibleThroughQueryFilter()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);

        var tenantId = Guid.NewGuid();
        var (service, _) = _db.BuildPersonService(tenantId);

        var created = await service.CreatePersonAsync(
            new Person { FirstName = "Han", LastName = "Solo" });

        // Visible to the correct tenant
        await using var correctCtx = _db.BuildContext(tenantId);
        var found = await correctCtx.People.FindAsync(created.Id);
        Assert.NotNull(found);
        Assert.Equal(tenantId, found.TenantId);

        // Invisible to a different tenant
        await using var wrongCtx = _db.BuildContext(Guid.NewGuid());
        var notFound = await wrongCtx.People.FindAsync(created.Id);
        Assert.Null(notFound);
    }

    // ── PersonService.AssignPersonToPartyAsync ────────────────────────────────

    /// <summary>
    /// <c>AssignPersonToPartyAsync</c> inserts directly into the <c>People</c>
    /// table (raw SQL) for a <c>Parties</c> row that already exists, without
    /// touching the <c>Parties</c> table a second time. The returned entity must
    /// carry the correct Id and TenantId.
    /// </summary>
    [SkippableFact]
    public async Task AssignPersonToPartyAsync_InsertsPersonRowLinkedToExistingParty()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);

        var tenantId = Guid.NewGuid();

        // Arrange — create an Organization so a Parties row already exists
        var (orgService, _) = _db.BuildOrganizationService(tenantId);
        var org = await orgService.CreateOrganizationAsync(
            new Organization { Name = "Rebel Alliance" });

        // Act — assign a Person subtype to that same Party identity
        var (personService, _) = _db.BuildPersonService(tenantId);
        var assigned = await personService.AssignPersonToPartyAsync(
            org.Id, new Person { FirstName = "Mon", LastName = "Mothma" });

        // Assert — People row was inserted with the correct Id
        await using var verify = _db.BuildContext(tenantId);
        var personRow = await verify.People
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == org.Id);

        Assert.NotNull(personRow);
        Assert.Equal(org.Id, assigned.Id);
        Assert.Equal(tenantId, assigned.TenantId);
        Assert.Equal("Mon", personRow.FirstName);
        Assert.Equal("Mothma", personRow.LastName);
    }

    /// <summary>
    /// <c>AssignPersonToPartyAsync</c> must throw when the target Party does not
    /// exist in the current tenant scope (either truly absent, or owned by another tenant).
    /// </summary>
    [SkippableFact]
    public async Task AssignPersonToPartyAsync_ThrowsWhenPartyDoesNotExist()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);

        var tenantId = Guid.NewGuid();
        var (service, _) = _db.BuildPersonService(tenantId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignPersonToPartyAsync(
                Guid.NewGuid(), // never persisted
                new Person { FirstName = "Ghost", LastName = "Nobody" }));
    }

    /// <summary>
    /// A Party created under Tenant B must be invisible to a service scoped to
    /// Tenant A. <c>ExistsAsync</c> uses the tenant-filtered context, so the
    /// assignment attempt must throw rather than cross the tenant boundary.
    /// </summary>
    [SkippableFact]
    public async Task AssignPersonToPartyAsync_ThrowsWhenPartyBelongsToADifferentTenant()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Create a Party under Tenant B
        var (orgServiceB, _) = _db.BuildOrganizationService(tenantB);
        var orgB = await orgServiceB.CreateOrganizationAsync(
            new Organization { Name = "Empire" });

        // Try to assign a Person from Tenant A's service to Tenant B's Party
        var (personServiceA, _) = _db.BuildPersonService(tenantA);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => personServiceA.AssignPersonToPartyAsync(
                orgB.Id, new Person { FirstName = "Darth", LastName = "Vader" }));
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    /// <summary>
    /// Seed People for two tenants in the same database and confirm the global
    /// query filter enforces a hard boundary: each tenant sees only its own rows,
    /// and the raw underlying count confirms no data was lost.
    /// </summary>
    [SkippableFact]
    public async Task PersonService_TenantIsolation_EachTenantSeesOnlyItsOwnData()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var (serviceA, _) = _db.BuildPersonService(tenantA);
        var (serviceB, _) = _db.BuildPersonService(tenantB);

        await serviceA.CreatePersonAsync(new Person { FirstName = "Luke", LastName = "Skywalker" });
        await serviceB.CreatePersonAsync(new Person { FirstName = "Darth", LastName = "Vader" });

        // Each tenant-scoped context returns only its own record
        await using var ctxA = _db.BuildContext(tenantA);
        await using var ctxB = _db.BuildContext(tenantB);

        var forA = await ctxA.People.ToListAsync();
        var forB = await ctxB.People.ToListAsync();

        Assert.Single(forA);
        Assert.Equal(tenantA, forA[0].TenantId);
        Assert.Equal("Luke", forA[0].FirstName);

        Assert.Single(forB);
        Assert.Equal(tenantB, forB[0].TenantId);
        Assert.Equal("Darth", forB[0].FirstName);

        // Confirm both rows exist in the database (filter hides, does not delete)
        await using var raw = _db.BuildContext(Guid.Empty);
        var both = await raw.People
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantA || p.TenantId == tenantB)
            .ToListAsync();
        Assert.Equal(2, both.Count);
    }

    // ── OrganizationService ───────────────────────────────────────────────────

    /// <summary>
    /// Same TPT verification as the Person test: a single <c>CreateOrganizationAsync</c>
    /// must produce rows in both <c>Parties</c> and <c>Organizations</c> tables,
    /// with the correct TenantId and Name.
    /// </summary>
    [SkippableFact]
    public async Task CreateOrganizationAsync_InsertsRowsInBothPartiesAndOrganizationsTables()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);

        var tenantId = Guid.NewGuid();
        var (service, _) = _db.BuildOrganizationService(tenantId);

        var created = await service.CreateOrganizationAsync(
            new Organization { Name = "Galactic Senate" });

        await using var verify = _db.BuildContext(tenantId);

        var partyRow = await verify.Parties
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == created.Id);

        var orgRow = await verify.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == created.Id);

        Assert.NotNull(partyRow);
        Assert.NotNull(orgRow);
        Assert.Equal(tenantId, partyRow.TenantId);
        Assert.Equal("Galactic Senate", orgRow.Name);
    }

    /// <summary>
    /// <c>AssignOrganizationToPartyAsync</c> must insert into <c>Organizations</c>
    /// only (raw SQL), linking to an existing <c>Parties</c> row without
    /// causing a primary-key violation on the base table.
    /// </summary>
    [SkippableFact]
    public async Task AssignOrganizationToPartyAsync_InsertsOrganizationRowLinkedToExistingParty()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);

        var tenantId = Guid.NewGuid();

        // Create a Person first so a Parties row exists
        var (personService, _) = _db.BuildPersonService(tenantId);
        var person = await personService.CreatePersonAsync(
            new Person { FirstName = "Padmé", LastName = "Amidala" });

        // Assign an Organization subtype to that Party identity
        var (orgService, _) = _db.BuildOrganizationService(tenantId);
        var assigned = await orgService.AssignOrganizationToPartyAsync(
            person.Id, new Organization { Name = "Naboo Royal House" });

        await using var verify = _db.BuildContext(tenantId);
        var orgRow = await verify.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == person.Id);

        Assert.NotNull(orgRow);
        Assert.Equal(person.Id, assigned.Id);
        Assert.Equal(tenantId, assigned.TenantId);
        Assert.Equal("Naboo Royal House", orgRow.Name);
    }
}
