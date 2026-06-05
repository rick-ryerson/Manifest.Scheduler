using Manifest.Scheduler.Domain.Common;

namespace Manifest.Scheduler.Domain.GalacticSenate.Entities;

/// <summary>
/// Classifies the function a Party plays within the system.
/// </summary>
public enum PartyRoleType
{
    Student = 1,
    Instructor = 2,
    Administrator = 3,
    Observer = 4
}

/// <summary>
/// Associates a Party with a role, allowing a single person or organization
/// to hold multiple roles (e.g., a person who is both an Instructor and Administrator).
/// </summary>
public class PartyRole : IMustHaveTenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    public Guid PartyId { get; set; }
    public Party Party { get; set; } = null!;

    public PartyRoleType RoleType { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
