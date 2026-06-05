using Manifest.Scheduler.Domain.Common;
using Microsoft.AspNetCore.Http;

namespace Manifest.Scheduler.Infrastructure.Services;

/// <summary>
/// Resolves the current tenant from the 'X-Tenant-Id' HTTP request header.
/// Falls back to Guid.Empty when no header is present (e.g. background jobs,
/// migrations, or unauthenticated requests).
/// </summary>
public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? CurrentTenantId
    {
        get
        {
            var header = _httpContextAccessor.HttpContext?
                .Request.Headers["X-Tenant-Id"]
                .FirstOrDefault();

            return Guid.TryParse(header, out var tenantId) ? tenantId : null;
        }
    }
}
