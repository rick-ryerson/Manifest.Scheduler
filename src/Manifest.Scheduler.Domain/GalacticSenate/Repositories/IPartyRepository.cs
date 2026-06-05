using Manifest.Scheduler.Domain.GalacticSenate.Entities;

namespace Manifest.Scheduler.Domain.GalacticSenate.Repositories;

/// <summary>
/// Cross-hierarchy repository for base Party identity.
///
/// Responsibilities:
///   - Answer "does this Party exist?" across all subtypes (Person, Organization)
///   - Retrieve the base Party record by ID without loading subtype data
///
/// Intentionally does NOT create parties. Creation is always done through a
/// subtype repository (IPersonRepository, IOrganizationRepository), which
/// produces the Parties row automatically via EF Core TPT.
///
/// This enforces the two-step pattern:
///   Step 1 — verify/locate identity:  IPartyRepository.GetByIdAsync / ExistsAsync
///   Step 2 — assign subtype data:     IPersonRepository.AddAsync / IOrganizationRepository.AddAsync
/// </summary>
public interface IPartyRepository
{
    /// <summary>Returns the base Party record if it exists, regardless of subtype.</summary>
    Task<Party?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns true if any Party with this ID exists in the hierarchy.</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all Party base records across the entire hierarchy.</summary>
    Task<List<Party>> GetAllAsync(CancellationToken ct = default);
}
