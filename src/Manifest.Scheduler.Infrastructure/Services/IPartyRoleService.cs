using Manifest.Scheduler.Domain.GalacticSenate.Entities;

namespace Manifest.Scheduler.Infrastructure.Services;

/// <summary>
/// Manages role assignments for Parties within the current tenant.
/// </summary>
public interface IPartyRoleService
{
    /// <summary>
    /// Assigns a role to an existing Party within the current tenant.
    /// Throws <see cref="InvalidOperationException"/> if the Party does not exist
    /// in the current tenant or if the tenant context is missing.
    /// </summary>
    Task<PartyRole> AssignRoleAsync(Guid partyId, PartyRoleType roleType, CancellationToken ct = default);

    /// <summary>Returns all roles assigned to the given Party within the current tenant.</summary>
    Task<List<PartyRole>> GetRolesForPartyAsync(Guid partyId, CancellationToken ct = default);

    /// <summary>Removes a role assignment. No-ops silently if the role does not exist.</summary>
    Task RemoveRoleAsync(Guid roleId, CancellationToken ct = default);
}
