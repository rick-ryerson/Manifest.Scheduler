using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Domain.GalacticSenate.Repositories;
using Manifest.Scheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Manifest.Scheduler.Infrastructure.Repositories;

public class PersonRepository : IPersonRepository
{
    private readonly ApplicationDbContext _context;

    public PersonRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Person?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.People.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<List<Person>> GetAllAsync(CancellationToken ct = default)
        => await _context.People.ToListAsync(ct);

    public async Task<List<Person>> FindByNameAsync(string firstName, string lastName, CancellationToken ct = default)
        => await _context.People
            .Where(p => p.FirstName == firstName && p.LastName == lastName)
            .ToListAsync(ct);

    public async Task<Person> AddAsync(Person person, CancellationToken ct = default)
    {
        _context.People.Add(person);
        await _context.SaveChangesAsync(ct);
        return person;
    }

    public async Task<Person> UpdateAsync(Person person, CancellationToken ct = default)
    {
        _context.People.Update(person);
        await _context.SaveChangesAsync(ct);
        return person;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var person = await _context.People.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (person is not null)
        {
            _context.People.Remove(person);
            await _context.SaveChangesAsync(ct);
        }
    }
}
