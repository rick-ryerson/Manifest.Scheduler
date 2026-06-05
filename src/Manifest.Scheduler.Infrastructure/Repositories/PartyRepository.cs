using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Domain.GalacticSenate.Repositories;
using Manifest.Scheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Manifest.Scheduler.Infrastructure.Repositories;

/// <summary>
/// Queries the Party hierarchy without loading subtype data.
/// Use IPersonRepository or IOrganizationRepository for subtype creation and management.
/// </summary>
public class PartyRepository : IPartyRepository
{
    private readonly ApplicationDbContext _context;

    public PartyRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Party?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Parties.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => await _context.Parties.AnyAsync(p => p.Id == id, ct);

    public async Task<List<Party>> GetAllAsync(CancellationToken ct = default)
        => await _context.Parties.ToListAsync(ct);
}
