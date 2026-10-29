using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.APIModels;
using DatabaseAPI.Services;

namespace DatabaseAPI.Controllers;

/// <summary>
/// Visits ("návštěvy") — the dated container replacing the flat
/// Documents/Events/Termíny tabs on the patient page.
/// </summary>
[ApiController]
[Route("api/patients/{patientId}/visits")]
public class VisitsController : ControllerBase
{
    private readonly IVisitService _visitService;
    private readonly IActorCountryContextService _actorCountryContextService;
    private readonly ILogger<VisitsController> _logger;

    public VisitsController(
        IVisitService visitService,
        IActorCountryContextService actorCountryContextService,
        ILogger<VisitsController> logger)
    {
        _visitService = visitService;
        _actorCountryContextService = actorCountryContextService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<VisitListItemDto>>> GetVisits(int patientId)
    {
        return Ok(await _visitService.GetVisitsAsync(patientId));
    }

    [HttpGet("{visitId}")]
    public async Task<ActionResult<VisitDetailDto>> GetVisit(int patientId, int visitId)
    {
        var visit = await _visitService.GetVisitAsync(patientId, visitId);
        return visit == null ? NotFound("Visit not found") : Ok(visit);
    }

    [HttpPost]
    public async Task<ActionResult<VisitDetailDto>> CreateVisit(int patientId, [FromBody] CreateVisitRequest request)
    {
        var countryCode = await _actorCountryContextService.GetActorCountryCodeAsync();
        var createdBy = _actorCountryContextService.GetActorEmployeeId();

        var visit = await _visitService.CreateVisitAsync(patientId, request, createdBy, countryCode);
        return visit == null
            ? NotFound("Patient not found")
            : CreatedAtAction(nameof(GetVisit), new { patientId, visitId = visit.Id }, visit);
    }

    [HttpPatch("{visitId}")]
    public async Task<ActionResult<VisitDetailDto>> UpdateVisit(int patientId, int visitId, [FromBody] UpdateVisitRequest request)
    {
        try
        {
            var visit = await _visitService.UpdateVisitAsync(patientId, visitId, request);
            return visit == null ? NotFound("Visit not found") : Ok(visit);
        }
        catch (ScopeViolationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Upload a file and attach it to the visit (multipart).</summary>
    [HttpPost("{visitId}/documents")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<VisitDocumentDto>> AttachDocument(int patientId, int visitId, IFormFile file)
    {
        if (file.Length == 0)
        {
            return BadRequest("Empty file");
        }

        try
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var document = await _visitService.AttachDocumentAsync(patientId, visitId, memoryStream.ToArray(), file.FileName);
            if (document == null)
            {
                return NotFound("Visit not found");
            }
            return CreatedAtAction(nameof(GetVisit), new { patientId, visitId }, document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error attaching document to visit {VisitId}", visitId);
            return StatusCode(500, "Error uploading document");
        }
    }
}
