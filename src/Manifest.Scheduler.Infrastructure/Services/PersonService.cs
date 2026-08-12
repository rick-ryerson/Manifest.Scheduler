using Manifest.Scheduler.Domain.Common;
using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Domain.GalacticSenate.Repositories;
using Manifest.Scheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Manifest.Scheduler.Infrastructure.Services;

public class PersonService : IPersonService
{
    private readonly IPersonRepository _personRepository;
    private readonly IPartyRepository _partyRepository;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;

    public PersonService(
        IPersonRepository personRepository,
        IPartyRepository partyRepository,
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService)
    {
        _personRepository = personRepository;
        _partyRepository = partyRepository;
        _context = context;
        _currentTenantService = currentTenantService;
    }

    /// <inheritdoc/>
    public async Task<Person> CreatePersonAsync(Person person, CancellationToken ct = default)
    {
        // Always stamp with the resolved tenant — callers must not control TenantId.
        person.TenantId = CurrentTenantIdOrThrow();

        // EF Core TPT: a single Add on People DbSet inserts into both
        // Parties (identity) and People (subtype) in one SaveChanges transaction.
        return await _personRepository.AddAsync(person, ct);
    }

    /// <inheritdoc/>
    public async Task<Person> AssignPersonToPartyAsync(Guid partyId, Person person, CancellationToken ct = default)
    {
        // ExistsAsync runs through the tenant-scoped query filter, so this also
        // verifies the Party belongs to the current tenant.
        if (!await _partyRepository.ExistsAsync(partyId, ct))
            throw new InvalidOperationException($"Party {partyId} does not exist.");

        // Set the subtype Id to match the existing Party identity.
        person.Id = partyId;

        // Reflect the tenant on the returned entity so callers don't see a stale value.
        person.TenantId = CurrentTenantIdOrThrow();

        // With TPT the Parties row already exists; TenantId lives in Parties (not People),
        // so we insert only the subtype columns into the People table.
        // ExecuteSqlAsync with a FormattableString uses parameterized SQL — no injection risk.
        // Double-quoted table name is ANSI SQL and works across SQL Server, PostgreSQL,
        // and SQLite. Unquoted "People" would be folded to lowercase by PostgreSQL and
        // fail to resolve the case-sensitive "People" table created by EF Core.
        await _context.Database.ExecuteSqlAsync(
            $"INSERT INTO \"People\" (\"Id\", \"FirstName\", \"LastName\") VALUES ({person.Id}, {person.FirstName}, {person.LastName})",
            ct);

        return person;
    }

    /// <inheritdoc/>
    public async Task<Person?> GetPersonByIdAsync(Guid id, CancellationToken ct = default)
        => await _personRepository.GetByIdAsync(id, ct);

    /// <inheritdoc/>
    public async Task<List<Person>> GetAllPeopleAsync(CancellationToken ct = default)
        => await _personRepository.GetAllAsync(ct);

    /// <inheritdoc/>
    public async Task<Person> UpdatePersonAsync(Guid id, Person person, CancellationToken ct = default)
    {
        // GetByIdAsync runs through the tenant-scoped query filter, so this also
        // verifies the Person belongs to the current tenant.
        var existing = await _personRepository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Person {id} does not exist.");

        existing.FirstName = person.FirstName;
        existing.LastName = person.LastName;

        return await _personRepository.UpdateAsync(existing, ct);
    }

    /// <inheritdoc/>
    public async Task DeletePersonAsync(Guid id, CancellationToken ct = default)
        => await _personRepository.DeleteAsync(id, ct);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Guid CurrentTenantIdOrThrow()
    {
        var tenantId = _currentTenantService.CurrentTenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException(
                "A valid tenant context is required. Ensure the X-Tenant-Id header is present.");
        return tenantId.Value;
    }
}
