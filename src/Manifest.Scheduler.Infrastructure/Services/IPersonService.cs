using Manifest.Scheduler.Domain.GalacticSenate.Entities;

namespace Manifest.Scheduler.Infrastructure.Services;

/// <summary>
/// Handles the two distinct operations for Person identity:
///   1. CreatePersonAsync  — new identity + Person subtype in one transaction
///   2. AssignPersonToPartyAsync — Person subtype applied to an existing Party identity
/// </summary>
public interface IPersonService
{
    /// <summary>
    /// Creates a new Party identity and Person subtype in a single EF Core transaction.
    /// EF Core TPT automatically inserts into both the Parties and People tables.
    /// </summary>
    Task<Person> CreatePersonAsync(Person person, CancellationToken ct = default);

    /// <summary>
    /// Links a Person role to an existing Party identity.
    /// Sets person.Id = partyId, then inserts directly into the People table
    /// since the Parties row already exists.
    /// </summary>
    Task<Person> AssignPersonToPartyAsync(Guid partyId, Person person, CancellationToken ct = default);
}
