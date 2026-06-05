using Manifest.Scheduler.Domain.GalacticSenate.Entities;

namespace Manifest.Scheduler.Domain.GalacticSenate.Repositories;

/// <summary>
/// Specialized repository for Person entities.
/// Inherits standard CRUD from IRepository&lt;Person&gt; and exposes
/// Person-specific query methods.
/// </summary>
public interface IPersonRepository : IRepository<Person>
{
    Task<List<Person>> FindByNameAsync(string firstName, string lastName, CancellationToken ct = default);
}
