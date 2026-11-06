using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
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
    private readonly IEmployeeScopeService _employeeScopeService;
    private readonly IIntakeFormLinkService _intakeFormLinkService;
    private readonly DatabaseContext _context;
    private readonly ILogger<VisitsController> _logger;
    private readonly IAntivirusScanner _antivirusScanner;

    public VisitsController(
        IVisitService visitService,
        IActorCountryContextService actorCountryContextService,
        IEmployeeScopeService employeeScopeService,
        IIntakeFormLinkService intakeFormLinkService,
        DatabaseContext context,
        ILogger<VisitsController> logger,
        IAntivirusScanner? antivirusScanner = null)
    {
        _visitService = visitService;
        _actorCountryContextService = actorCountryContextService;
        _employeeScopeService = employeeScopeService;
        _intakeFormLinkService = intakeFormLinkService;
        _context = context;
        _logger = logger;
        _antivirusScanner = antivirusScanner ?? new NullAntivirusScanner();
    }

    [HttpGet]
    public async Task<ActionResult<List<VisitListItemDto>>> GetVisits(int patientId)
    {
        if (!await PatientIsVisibleAsync(patientId)) return NotFound("Patient not found");
        return Ok(await _visitService.GetVisitsAsync(patientId));
    }

    [HttpGet("{visitId}")]
    public async Task<ActionResult<VisitDetailDto>> GetVisit(int patientId, int visitId)
    {
        if (!await PatientIsVisibleAsync(patientId)) return NotFound("Patient not found");
        var visit = await _visitService.GetVisitAsync(patientId, visitId);
        return visit == null ? NotFound("Visit not found") : Ok(visit);
    }

    [HttpPost]
    public async Task<ActionResult<VisitDetailDto>> CreateVisit(int patientId, [FromBody] CreateVisitRequest request)
    {
        if (!await PatientIsVisibleAsync(patientId)) return NotFound("Patient not found");
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
            if (!await PatientIsVisibleAsync(patientId)) return NotFound("Patient not found");
            var visit = await _visitService.UpdateVisitAsync(patientId, visitId, request);
            return visit == null ? NotFound("Visit not found") : Ok(visit);
        }
        catch (ScopeViolationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Creates a link to the public intake form for this visit. The doctor
    /// opens it on a tablet and hands it to the patient; submissions attach
    /// to this visit. The link stays valid through the visit day.
    /// </summary>
    [HttpPost("{visitId}/intake-link")]
    public async Task<ActionResult<IntakeFormLinkDto>> CreateIntakeLink(int patientId, int visitId)
    {
        if (!await PatientIsVisibleAsync(patientId)) return NotFound("Patient not found");

        var visit = await _context.Visits
            .FirstOrDefaultAsync(v => v.Id == visitId && v.PatientId == patientId);
        if (visit == null)
        {
            return NotFound("Visit not found");
        }

        var patient = await _context.Patients
            .Include(p => p.Person)
            .FirstOrDefaultAsync(p => p.Id == patientId);
        if (patient == null)
        {
            return NotFound("Patient not found");
        }

        var expiresAt = visit.VisitDate.Date.AddDays(1);
        if (expiresAt < DateTime.UtcNow.AddHours(2))
        {
            expiresAt = DateTime.UtcNow.AddHours(2);
        }

        var created = await _intakeFormLinkService.CreateLinkForVisitAsync(
            patient.PersonId, visit.Id, visit.ReservationSlotId, expiresAt);
        if (created == null)
        {
            return BadRequest("Could not create an intake form link");
        }

        var (link, rawToken) = created.Value;
        return Ok(new IntakeFormLinkDto
        {
            LinkId = link.Id,
            Token = rawToken,
            IntakePath = $"/intake/{rawToken}",
            ExpiresAtUtc = link.ExpiresAtUtc,
            PersonId = link.PersonId,
            PersonName = $"{patient.Person.FirstName} {patient.Person.LastName}".Trim(),
            ReservationId = null
        });
    }

    private async Task<bool> PatientIsVisibleAsync(int patientId)
    {
        var actorScope = await _employeeScopeService.GetActorScopeAsync();
        return await DatabaseAPI.Utils.PatientScope
            .Apply(_context.Patients, actorScope)
            .AnyAsync(p => p.Id == patientId && !p.IsDeleted);
    }

    /// <summary>Upload a file and attach it to the visit (multipart).</summary>
    [HttpPost("{visitId}/documents")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<VisitDocumentDto>> AttachDocument(int patientId, int visitId, IFormFile file)
    {
        if (!await PatientIsVisibleAsync(patientId)) return NotFound("Patient not found");

        if (file.Length == 0)
        {
            return BadRequest("Empty file");
        }

        try
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var documentData = memoryStream.ToArray();

            var scan = await _antivirusScanner.ScanAsync(documentData);
            if (!scan.Clean)
            {
                if (scan.ThreatName != null)
                {
                    _logger.LogWarning("Rejected malicious visit upload {VisitId}: {Threat}", visitId, scan.ThreatName);
                    return BadRequest("File failed antivirus scan.");
                }

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    scan.Error ?? "Antivirus scan is unavailable; the file was not stored.");
            }

            var document = await _visitService.AttachDocumentAsync(patientId, visitId, documentData, file.FileName);
            if (document == null)
            {
                return NotFound("Visit not found");
            }
            return CreatedAtAction(nameof(GetVisit), new { patientId, visitId }, document);
        }
        catch (InvalidOperationException ex)
        {
            // e.g. an invalid/corrupt .tdb measurement
            _logger.LogWarning(ex, "Rejected document for visit {VisitId}", visitId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error attaching document to visit {VisitId}", visitId);
            return StatusCode(500, "Error uploading document");
        }
    }
}
