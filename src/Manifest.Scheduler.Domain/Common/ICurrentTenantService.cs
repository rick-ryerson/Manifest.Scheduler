namespace Manifest.Scheduler.Domain.Common;

public interface ICurrentTenantService
{
    Guid? CurrentTenantId { get; }
}
