using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Manifest.Scheduler.Infrastructure.Repositories;

/// <summary>
/// Provides CRUD operations for the Party hierarchy (Person and Organization).
/// All queries are automatically tenant-scoped via the ApplicationDbContext
/// global query filter — no manual TenantId filtering is required here.
/// </summary>
public class PartyRepository
{
    private readonly ApplicationDbContext _context;

    public PartyRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Queries ────────────────────────────────────────────────────────────────

    public async Task<Party?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Parties.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<List<Person>> GetAllPeopleAsync(CancellationToken ct = default)
        => await _context.People.ToListAsync(ct);

    public async Task<List<Organization>> GetAllOrganizationsAsync(CancellationToken ct = default)
        => await _context.Organizations.ToListAsync(ct);

    // ── Commands ───────────────────────────────────────────────────────────────

    public async Task<Person> AddPersonAsync(Person person, CancellationToken ct = default)
    {
        _context.People.Add(person);
        await _context.SaveChangesAsync(ct);
        return person;
    }

    public async Task<Organization> AddOrganizationAsync(Organization organization, CancellationToken ct = default)
    {
        _context.Organizations.Add(organization);
        await _context.SaveChangesAsync(ct);
        return organization;
    }

    public async Task<Party> UpdateAsync(Party party, CancellationToken ct = default)
    {
        _context.Parties.Update(party);
        await _context.SaveChangesAsync(ct);
        return party;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var party = await _context.Parties.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (party is not null)
        {
            _context.Parties.Remove(party);
            await _context.SaveChangesAsync(ct);
        }
    }
}
