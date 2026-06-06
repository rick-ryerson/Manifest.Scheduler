using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Domain.GalacticSenate.Repositories;
using Manifest.Scheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Manifest.Scheduler.Infrastructure.Repositories;

public class PartyRoleRepository : IPartyRoleRepository
{
    private readonly ApplicationDbContext _context;

    public PartyRoleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PartyRole?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PartyRoles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<List<PartyRole>> GetByPartyIdAsync(Guid partyId, CancellationToken ct = default)
        => await _context.PartyRoles.Where(r => r.PartyId == partyId).ToListAsync(ct);

    public async Task<List<PartyRole>> GetAllAsync(CancellationToken ct = default)
        => await _context.PartyRoles.ToListAsync(ct);

    public async Task<PartyRole> AddAsync(PartyRole role, CancellationToken ct = default)
    {
        _context.PartyRoles.Add(role);
        await _context.SaveChangesAsync(ct);
        return role;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _context.PartyRoles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is not null)
        {
            _context.PartyRoles.Remove(role);
            await _context.SaveChangesAsync(ct);
        }
    }
}
