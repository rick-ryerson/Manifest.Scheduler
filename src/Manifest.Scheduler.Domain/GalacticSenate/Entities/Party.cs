using Manifest.Scheduler.Domain.Common;

namespace Manifest.Scheduler.Domain.GalacticSenate.Entities;

/// <summary>
/// Abstract base for all first-class participants in the system —
/// both people and organizations.
/// TenantId is declared here so every derived type (Person, Organization)
/// is automatically tenant-scoped without repeating the property.
/// </summary>
public abstract class Party : IMustHaveTenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
