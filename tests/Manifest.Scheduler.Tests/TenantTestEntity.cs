using Manifest.Scheduler.Domain.Common;

namespace Manifest.Scheduler.Tests;

/// <summary>
/// Verification-only entity used to test the ApplicationDbContext global query filter.
/// Lives in the test project to keep production domain models clean.
/// </summary>
public class TenantTestEntity : IMustHaveTenant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
}
