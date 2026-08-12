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

    /// <summary>Returns the Person with the given Id, or null if it doesn't exist in the current tenant.</summary>
    Task<Person?> GetPersonByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all People in the current tenant.</summary>
    Task<List<Person>> GetAllPeopleAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates FirstName/LastName for an existing Person.
    /// Throws <see cref="InvalidOperationException"/> if the Person does not exist in the current tenant.
    /// </summary>
    Task<Person> UpdatePersonAsync(Guid id, Person person, CancellationToken ct = default);

    /// <summary>
    /// Deletes a Person (and its underlying Party identity). No-ops silently if it does not exist.
    /// Associated PartyRoles are removed via ON DELETE CASCADE.
    /// </summary>
    Task DeletePersonAsync(Guid id, CancellationToken ct = default);
}
