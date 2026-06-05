using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Domain.GalacticSenate.Repositories;
using Manifest.Scheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Manifest.Scheduler.Infrastructure.Services;

public class PersonService : IPersonService
{
    private readonly IPersonRepository _personRepository;
    private readonly IPartyRepository _partyRepository;
    private readonly ApplicationDbContext _context;

    public PersonService(
        IPersonRepository personRepository,
        IPartyRepository partyRepository,
        ApplicationDbContext context)
    {
        _personRepository = personRepository;
        _partyRepository = partyRepository;
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<Person> CreatePersonAsync(Person person, CancellationToken ct = default)
    {
        // EF Core TPT: a single Add on People DbSet inserts into both
        // Parties (identity) and People (subtype) in one SaveChanges transaction.
        return await _personRepository.AddAsync(person, ct);
    }

    /// <inheritdoc/>
    public async Task<Person> AssignPersonToPartyAsync(Guid partyId, Person person, CancellationToken ct = default)
    {
        if (!await _partyRepository.ExistsAsync(partyId, ct))
            throw new InvalidOperationException($"Party {partyId} does not exist.");

        // Set the subtype Id to match the existing Party identity.
        person.Id = partyId;

        // With TPT the Parties row already exists, so we insert only into the
        // People table directly. Using EF's Add would attempt to insert into
        // both tables and fail with a PK violation on Parties.
        await _context.Database.ExecuteSqlAsync(
            $"INSERT INTO People (Id, FirstName, LastName) VALUES ({person.Id}, {person.FirstName}, {person.LastName})",
            ct);

        return person;
    }
}
