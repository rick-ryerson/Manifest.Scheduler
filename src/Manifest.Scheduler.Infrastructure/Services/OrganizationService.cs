using Manifest.Scheduler.Domain.Common;
using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Domain.GalacticSenate.Repositories;
using Manifest.Scheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Manifest.Scheduler.Infrastructure.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPartyRepository _partyRepository;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;

    public OrganizationService(
        IOrganizationRepository organizationRepository,
        IPartyRepository partyRepository,
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService)
    {
        _organizationRepository = organizationRepository;
        _partyRepository = partyRepository;
        _context = context;
        _currentTenantService = currentTenantService;
    }

    /// <inheritdoc/>
    public async Task<Organization> CreateOrganizationAsync(Organization organization, CancellationToken ct = default)
    {
        // Always stamp with the resolved tenant — callers must not control TenantId.
        organization.TenantId = CurrentTenantIdOrThrow();

        // EF Core TPT: a single Add on Organizations DbSet inserts into both
        // Parties (identity) and Organizations (subtype) in one SaveChanges transaction.
        return await _organizationRepository.AddAsync(organization, ct);
    }

    /// <inheritdoc/>
    public async Task<Organization> AssignOrganizationToPartyAsync(Guid partyId, Organization organization, CancellationToken ct = default)
    {
        // ExistsAsync runs through the tenant-scoped query filter, so this also
        // verifies the Party belongs to the current tenant.
        if (!await _partyRepository.ExistsAsync(partyId, ct))
            throw new InvalidOperationException($"Party {partyId} does not exist.");

        // Set the subtype Id to match the existing Party identity.
        organization.Id = partyId;

        // Reflect the tenant on the returned entity so callers don't see a stale value.
        organization.TenantId = CurrentTenantIdOrThrow();

        // With TPT the Parties row already exists; TenantId lives in Parties (not Organizations),
        // so we insert only the subtype columns into the Organizations table.
        // ExecuteSqlAsync with a FormattableString uses parameterized SQL — no injection risk.
        await _context.Database.ExecuteSqlAsync(
            $"INSERT INTO Organizations (Id, Name) VALUES ({organization.Id}, {organization.Name})",
            ct);

        return organization;
    }

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
