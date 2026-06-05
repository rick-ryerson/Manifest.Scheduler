namespace Manifest.Scheduler.Domain.Common;

public interface IMustHaveTenant
{
    Guid TenantId { get; set; }
}
