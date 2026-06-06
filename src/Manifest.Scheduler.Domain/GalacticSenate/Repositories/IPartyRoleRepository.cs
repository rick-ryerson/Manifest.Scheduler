using Manifest.Scheduler.Domain.GalacticSenate.Entities;

namespace Manifest.Scheduler.Domain.GalacticSenate.Repositories;

/// <summary>
/// Manages the lifecycle of PartyRole records within the current tenant.
/// Roles are always scoped to the calling tenant via the global query filter.
/// </summary>
public interface IPartyRoleRepository
{
    Task<PartyRole?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all roles assigned to a specific Party within the current tenant.</summary>
    Task<List<PartyRole>> GetByPartyIdAsync(Guid partyId, CancellationToken ct = default);

    Task<List<PartyRole>> GetAllAsync(CancellationToken ct = default);

    Task<PartyRole> AddAsync(PartyRole role, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
