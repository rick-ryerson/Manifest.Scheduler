using Manifest.Scheduler.Domain.GalacticSenate.Entities;
using Manifest.Scheduler.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Manifest.Scheduler.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PartyController : ControllerBase
{
    private readonly IPersonService _personService;
    private readonly IOrganizationService _organizationService;
    private readonly IPartyRoleService _partyRoleService;
    private readonly ILogger<PartyController> _logger;

    public PartyController(
        IPersonService personService,
        IOrganizationService organizationService,
        IPartyRoleService partyRoleService,
        ILogger<PartyController> logger)
    {
        _personService = personService;
        _organizationService = organizationService;
        _partyRoleService = partyRoleService;
        _logger = logger;
    }

    // ── Person endpoints ───────────────────────────────────────────────────────

    /// <summary>Creates a new Party identity and Person subtype in one transaction.</summary>
    /// <response code="201">Person created successfully.</response>
    /// <response code="400">Missing or invalid tenant context, or bad request body.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("people")]
    [ProducesResponseType(typeof(Person), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreatePerson([FromBody] Person person, CancellationToken ct)
    {
        try
        {
            var created = await _personService.CreatePersonAsync(person, ct);
            return CreatedAtAction(nameof(CreatePerson), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Bad request creating Person");
            return BadRequest(Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating Person");
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>Assigns a Person subtype to an existing Party identity.</summary>
    /// <response code="200">Person assigned successfully.</response>
    /// <response code="400">Missing or invalid tenant context.</response>
    /// <response code="404">The specified Party does not exist in the current tenant.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("people/{partyId:guid}/assign")]
    [ProducesResponseType(typeof(Person), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AssignPerson(Guid partyId, [FromBody] Person person, CancellationToken ct)
    {
        try
        {
            var result = await _personService.AssignPersonToPartyAsync(partyId, person, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist"))
        {
            _logger.LogWarning(ex, "Party {PartyId} not found for Person assignment", partyId);
            return NotFound(Problem(ex.Message, statusCode: StatusCodes.Status404NotFound));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Bad request assigning Person to Party {PartyId}", partyId);
            return BadRequest(Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error assigning Person to Party {PartyId}", partyId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }

    // ── Organization endpoints ─────────────────────────────────────────────────

    /// <summary>Creates a new Party identity and Organization subtype in one transaction.</summary>
    /// <response code="201">Organization created successfully.</response>
    /// <response code="400">Missing or invalid tenant context, or bad request body.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("organizations")]
    [ProducesResponseType(typeof(Organization), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrganization([FromBody] Organization organization, CancellationToken ct)
    {
        try
        {
            var created = await _organizationService.CreateOrganizationAsync(organization, ct);
            return CreatedAtAction(nameof(CreateOrganization), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Bad request creating Organization");
            return BadRequest(Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating Organization");
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>Assigns an Organization subtype to an existing Party identity.</summary>
    /// <response code="200">Organization assigned successfully.</response>
    /// <response code="400">Missing or invalid tenant context.</response>
    /// <response code="404">The specified Party does not exist in the current tenant.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("organizations/{partyId:guid}/assign")]
    [ProducesResponseType(typeof(Organization), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AssignOrganization(Guid partyId, [FromBody] Organization organization, CancellationToken ct)
    {
        try
        {
            var result = await _organizationService.AssignOrganizationToPartyAsync(partyId, organization, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist"))
        {
            _logger.LogWarning(ex, "Party {PartyId} not found for Organization assignment", partyId);
            return NotFound(Problem(ex.Message, statusCode: StatusCodes.Status404NotFound));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Bad request assigning Organization to Party {PartyId}", partyId);
            return BadRequest(Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error assigning Organization to Party {PartyId}", partyId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }

    // ── PartyRole endpoints ────────────────────────────────────────────────────

    /// <summary>Assigns a role to an existing Party within the current tenant.</summary>
    /// <response code="201">Role assigned successfully.</response>
    /// <response code="400">Missing or invalid tenant context.</response>
    /// <response code="404">The specified Party does not exist in the current tenant.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("{partyId:guid}/roles")]
    [ProducesResponseType(typeof(PartyRole), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AssignRole(Guid partyId, [FromBody] PartyRoleType roleType, CancellationToken ct)
    {
        try
        {
            var created = await _partyRoleService.AssignRoleAsync(partyId, roleType, ct);
            return CreatedAtAction(nameof(GetRoles), new { partyId }, created);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist"))
        {
            _logger.LogWarning(ex, "Party {PartyId} not found for role assignment", partyId);
            return NotFound(Problem(ex.Message, statusCode: StatusCodes.Status404NotFound));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Bad request assigning role to Party {PartyId}", partyId);
            return BadRequest(Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error assigning role to Party {PartyId}", partyId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>Returns all roles assigned to a Party within the current tenant.</summary>
    /// <response code="200">List of roles (may be empty).</response>
    /// <response code="400">Missing or invalid tenant context.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("{partyId:guid}/roles")]
    [ProducesResponseType(typeof(List<PartyRole>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRoles(Guid partyId, CancellationToken ct)
    {
        try
        {
            var roles = await _partyRoleService.GetRolesForPartyAsync(partyId, ct);
            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving roles for Party {PartyId}", partyId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>Removes a role assignment.</summary>
    /// <response code="204">Role removed (or did not exist).</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpDelete("roles/{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveRole(Guid roleId, CancellationToken ct)
    {
        try
        {
            await _partyRoleService.RemoveRoleAsync(roleId, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error removing role {RoleId}", roleId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }
}
