using Manifest.Scheduler.Domain.GalacticSenate.Entities;

namespace Manifest.Scheduler.Domain.GalacticSenate.Repositories;

/// <summary>
/// Generic CRUD contract for any entity in the Party hierarchy.
/// Implementations are provided by Infrastructure and injected via DI.
/// </summary>
public interface IRepository<T> where T : Party
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<T>> GetAllAsync(CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task<T> UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
