using Manifest.Scheduler.Domain.Common;
using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Domain.GalacticSenate.Repositories;

namespace Manifest.Scheduler.Infrastructure.Services;

public class PartyRoleService : IPartyRoleService
{
    private readonly IPartyRoleRepository _partyRoleRepository;
    private readonly IPartyRepository _partyRepository;
    private readonly ICurrentTenantService _currentTenantService;

    public PartyRoleService(
        IPartyRoleRepository partyRoleRepository,
        IPartyRepository partyRepository,
        ICurrentTenantService currentTenantService)
    {
        _partyRoleRepository = partyRoleRepository;
        _partyRepository = partyRepository;
        _currentTenantService = currentTenantService;
    }

    /// <inheritdoc/>
    public async Task<PartyRole> AssignRoleAsync(Guid partyId, PartyRoleType roleType, CancellationToken ct = default)
    {
        var tenantId = CurrentTenantIdOrThrow();

        // ExistsAsync runs through the tenant-scoped query filter, implicitly
        // verifying both existence and tenant ownership in one round-trip.
        if (!await _partyRepository.ExistsAsync(partyId, ct))
            throw new InvalidOperationException($"Party {partyId} does not exist.");

        var role = new PartyRole
        {
            PartyId = partyId,
            RoleType = roleType,
            TenantId = tenantId,
            AssignedAt = DateTime.UtcNow
        };

        return await _partyRoleRepository.AddAsync(role, ct);
    }

    /// <inheritdoc/>
    public async Task<List<PartyRole>> GetRolesForPartyAsync(Guid partyId, CancellationToken ct = default)
        => await _partyRoleRepository.GetByPartyIdAsync(partyId, ct);

    /// <inheritdoc/>
    public async Task RemoveRoleAsync(Guid roleId, CancellationToken ct = default)
        => await _partyRoleRepository.DeleteAsync(roleId, ct);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Guid CurrentTenantIdOrThrow()
    {
        var tenantId = _currentTenantService.CurrentTenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            throw new InvalidOperationException(
                "A valid tenant context is required. Ensure the X-Tenant-Id header is present.");
        return tenantId.Value;
    }
}
