using Manifest.Scheduler.Domain.GalacticSenate.Entities;

namespace Manifest.Scheduler.Domain.GalacticSenate.Repositories;

/// <summary>
/// Specialized repository for Organization entities.
/// Inherits standard CRUD from IRepository&lt;Organization&gt; and exposes
/// Organization-specific query methods.
/// </summary>
public interface IOrganizationRepository : IRepository<Organization>
{
    Task<Organization?> FindByNameAsync(string name, CancellationToken ct = default);
}
