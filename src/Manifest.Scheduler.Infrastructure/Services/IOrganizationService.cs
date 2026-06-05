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
}
