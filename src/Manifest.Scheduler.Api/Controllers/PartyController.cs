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

    /// <summary>Returns the Person with the given Id.</summary>
    /// <response code="200">Person found.</response>
    /// <response code="404">No Person with that Id exists in the current tenant.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("people/{id:guid}")]
    [ProducesResponseType(typeof(Person), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPerson(Guid id, CancellationToken ct)
    {
        try
        {
            var person = await _personService.GetPersonByIdAsync(id, ct);
            return person is null
                ? NotFound(Problem($"Person {id} does not exist.", statusCode: StatusCodes.Status404NotFound))
                : Ok(person);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving Person {PersonId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>Returns all People in the current tenant.</summary>
    /// <response code="200">List of people (may be empty).</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("people")]
    [ProducesResponseType(typeof(List<Person>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllPeople(CancellationToken ct)
    {
        try
        {
            var people = await _personService.GetAllPeopleAsync(ct);
            return Ok(people);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving People");
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>Updates FirstName/LastName for an existing Person.</summary>
    /// <response code="200">Person updated successfully.</response>
    /// <response code="400">Missing or invalid tenant context.</response>
    /// <response code="404">No Person with that Id exists in the current tenant.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPut("people/{id:guid}")]
    [ProducesResponseType(typeof(Person), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdatePerson(Guid id, [FromBody] Person person, CancellationToken ct)
    {
        try
        {
            var updated = await _personService.UpdatePersonAsync(id, person, ct);
            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist"))
        {
            _logger.LogWarning(ex, "Person {PersonId} not found for update", id);
            return NotFound(Problem(ex.Message, statusCode: StatusCodes.Status404NotFound));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Bad request updating Person {PersonId}", id);
            return BadRequest(Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating Person {PersonId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>Deletes a Person and its underlying Party identity. Associated roles are cascade-deleted.</summary>
    /// <response code="204">Person deleted (or did not exist).</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpDelete("people/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeletePerson(Guid id, CancellationToken ct)
    {
        try
        {
            await _personService.DeletePersonAsync(id, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting Person {PersonId}", id);
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

    /// <summary>Returns the Organization with the given Id.</summary>
    /// <response code="200">Organization found.</response>
    /// <response code="404">No Organization with that Id exists in the current tenant.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("organizations/{id:guid}")]
    [ProducesResponseType(typeof(Organization), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOrganization(Guid id, CancellationToken ct)
    {
        try
        {
            var organization = await _organizationService.GetOrganizationByIdAsync(id, ct);
            return organization is null
                ? NotFound(Problem($"Organization {id} does not exist.", statusCode: StatusCodes.Status404NotFound))
                : Ok(organization);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving Organization {OrganizationId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>Returns all Organizations in the current tenant.</summary>
    /// <response code="200">List of organizations (may be empty).</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("organizations")]
    [ProducesResponseType(typeof(List<Organization>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllOrganizations(CancellationToken ct)
    {
        try
        {
            var organizations = await _organizationService.GetAllOrganizationsAsync(ct);
            return Ok(organizations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving Organizations");
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>Updates Name for an existing Organization.</summary>
    /// <response code="200">Organization updated successfully.</response>
    /// <response code="400">Missing or invalid tenant context.</response>
    /// <response code="404">No Organization with that Id exists in the current tenant.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPut("organizations/{id:guid}")]
    [ProducesResponseType(typeof(Organization), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateOrganization(Guid id, [FromBody] Organization organization, CancellationToken ct)
    {
        try
        {
            var updated = await _organizationService.UpdateOrganizationAsync(id, organization, ct);
            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist"))
        {
            _logger.LogWarning(ex, "Organization {OrganizationId} not found for update", id);
            return NotFound(Problem(ex.Message, statusCode: StatusCodes.Status404NotFound));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Bad request updating Organization {OrganizationId}", id);
            return BadRequest(Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating Organization {OrganizationId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }

    /// <summary>Deletes an Organization and its underlying Party identity. Associated roles are cascade-deleted.</summary>
    /// <response code="204">Organization deleted (or did not exist).</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpDelete("organizations/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteOrganization(Guid id, CancellationToken ct)
    {
        try
        {
            await _organizationService.DeleteOrganizationAsync(id, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting Organization {OrganizationId}", id);
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
