using Manifest.Scheduler.Domain.GalacticSenate.Entities;

namespace Manifest.Scheduler.Infrastructure.Services;

/// <summary>
/// Handles the two distinct operations for Organization identity:
///   1. CreateOrganizationAsync  — new identity + Organization subtype in one transaction
///   2. AssignOrganizationToPartyAsync — Organization subtype applied to an existing Party identity
/// </summary>
public interface IOrganizationService
{
    /// <summary>
    /// Creates a new Party identity and Organization subtype in a single EF Core transaction.
    /// EF Core TPT automatically inserts into both the Parties and Organizations tables.
    /// </summary>
    Task<Organization> CreateOrganizationAsync(Organization organization, CancellationToken ct = default);

    /// <summary>
    /// Links an Organization role to an existing Party identity.
    /// Sets organization.Id = partyId, then inserts directly into the Organizations table
    /// since the Parties row already exists.
    /// </summary>
    Task<Organization> AssignOrganizationToPartyAsync(Guid partyId, Organization organization, CancellationToken ct = default);

    /// <summary>Returns the Organization with the given Id, or null if it doesn't exist in the current tenant.</summary>
    Task<Organization?> GetOrganizationByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all Organizations in the current tenant.</summary>
    Task<List<Organization>> GetAllOrganizationsAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates Name for an existing Organization.
    /// Throws <see cref="InvalidOperationException"/> if the Organization does not exist in the current tenant.
    /// </summary>
    Task<Organization> UpdateOrganizationAsync(Guid id, Organization organization, CancellationToken ct = default);

    /// <summary>
    /// Deletes an Organization (and its underlying Party identity). No-ops silently if it does not exist.
    /// Associated PartyRoles are removed via ON DELETE CASCADE.
    /// </summary>
    Task DeleteOrganizationAsync(Guid id, CancellationToken ct = default);
}
