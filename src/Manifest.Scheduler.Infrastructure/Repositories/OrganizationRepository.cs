using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Domain.GalacticSenate.Repositories;
using Manifest.Scheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Manifest.Scheduler.Infrastructure.Repositories;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly ApplicationDbContext _context;

    public OrganizationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<List<Organization>> GetAllAsync(CancellationToken ct = default)
        => await _context.Organizations.ToListAsync(ct);

    public async Task<Organization?> FindByNameAsync(string name, CancellationToken ct = default)
        => await _context.Organizations.FirstOrDefaultAsync(o => o.Name == name, ct);

    public async Task<Organization> AddAsync(Organization organization, CancellationToken ct = default)
    {
        _context.Organizations.Add(organization);
        await _context.SaveChangesAsync(ct);
        return organization;
    }

    public async Task<Organization> UpdateAsync(Organization organization, CancellationToken ct = default)
    {
        _context.Organizations.Update(organization);
        await _context.SaveChangesAsync(ct);
        return organization;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var organization = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (organization is not null)
        {
            _context.Organizations.Remove(organization);
            await _context.SaveChangesAsync(ct);
        }
    }
}
