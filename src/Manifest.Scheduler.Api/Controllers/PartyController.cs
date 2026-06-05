using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Manifest.Scheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PartyController : ControllerBase
{
    private readonly IPersonService _personService;
    private readonly IOrganizationService _organizationService;

    public PartyController(IPersonService personService, IOrganizationService organizationService)
    {
        _personService = personService;
        _organizationService = organizationService;
    }

    // ── Person endpoints ───────────────────────────────────────────────────────

    /// <summary>Creates a new Party identity and Person subtype in one transaction.</summary>
    [HttpPost("people")]
    public async Task<IActionResult> CreatePerson([FromBody] Person person, CancellationToken ct)
    {
        var created = await _personService.CreatePersonAsync(person, ct);
        return CreatedAtAction(nameof(CreatePerson), new { id = created.Id }, created);
    }

    /// <summary>Assigns a Person subtype to an existing Party identity.</summary>
    [HttpPost("people/{partyId:guid}/assign")]
    public async Task<IActionResult> AssignPerson(Guid partyId, [FromBody] Person person, CancellationToken ct)
    {
        var result = await _personService.AssignPersonToPartyAsync(partyId, person, ct);
        return Ok(result);
    }

    // ── Organization endpoints ─────────────────────────────────────────────────

    /// <summary>Creates a new Party identity and Organization subtype in one transaction.</summary>
    [HttpPost("organizations")]
    public async Task<IActionResult> CreateOrganization([FromBody] Organization organization, CancellationToken ct)
    {
        var created = await _organizationService.CreateOrganizationAsync(organization, ct);
        return CreatedAtAction(nameof(CreateOrganization), new { id = created.Id }, created);
    }

    /// <summary>Assigns an Organization subtype to an existing Party identity.</summary>
    [HttpPost("organizations/{partyId:guid}/assign")]
    public async Task<IActionResult> AssignOrganization(Guid partyId, [FromBody] Organization organization, CancellationToken ct)
    {
        var result = await _organizationService.AssignOrganizationToPartyAsync(partyId, organization, ct);
        return Ok(result);
    }
}
