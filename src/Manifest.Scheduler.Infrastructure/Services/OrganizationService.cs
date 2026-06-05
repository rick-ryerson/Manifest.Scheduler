using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Domain.GalacticSenate.Repositories;
using Manifest.Scheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Manifest.Scheduler.Infrastructure.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPartyRepository _partyRepository;
    private readonly ApplicationDbContext _context;

    public OrganizationService(
        IOrganizationRepository organizationRepository,
        IPartyRepository partyRepository,
        ApplicationDbContext context)
    {
        _organizationRepository = organizationRepository;
        _partyRepository = partyRepository;
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<Organization> CreateOrganizationAsync(Organization organization, CancellationToken ct = default)
    {
        // EF Core TPT: a single Add on Organizations DbSet inserts into both
        // Parties (identity) and Organizations (subtype) in one SaveChanges transaction.
        return await _organizationRepository.AddAsync(organization, ct);
    }

    /// <inheritdoc/>
    public async Task<Organization> AssignOrganizationToPartyAsync(Guid partyId, Organization organization, CancellationToken ct = default)
    {
        if (!await _partyRepository.ExistsAsync(partyId, ct))
            throw new InvalidOperationException($"Party {partyId} does not exist.");

        // Set the subtype Id to match the existing Party identity.
        organization.Id = partyId;

        // With TPT the Parties row already exists, so we insert only into the
        // Organizations table directly. Using EF's Add would attempt to insert
        // into both tables and fail with a PK violation on Parties.
        await _context.Database.ExecuteSqlAsync(
            $"INSERT INTO Organizations (Id, Name) VALUES ({organization.Id}, {organization.Name})",
            ct);

        return organization;
    }
}
