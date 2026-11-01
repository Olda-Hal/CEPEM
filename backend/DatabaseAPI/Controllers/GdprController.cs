using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers;

/// <summary>
/// GDPR tooling: data map, full export (JSON/ZIP), anonymization, consent
/// templates and recorded patient consents. Gateway-gated by gdpr.manage.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GdprController : ControllerBase
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly DatabaseContext _context;
    private readonly IGdprService _gdprService;
    private readonly IActorCountryContextService _actorCountryContextService;
    private readonly ILogger<GdprController> _logger;

    public GdprController(
        DatabaseContext context,
        IGdprService gdprService,
        IActorCountryContextService actorCountryContextService,
        ILogger<GdprController> logger)
    {
        _context = context;
        _gdprService = gdprService;
        _actorCountryContextService = actorCountryContextService;
        _logger = logger;
    }

    [HttpGet("patients/{patientId:int}/data-map")]
    public async Task<ActionResult<GdprDataMapDto>> GetDataMap(int patientId)
    {
        var map = await _gdprService.GetDataMapAsync(patientId);
        return map == null ? NotFound("Patient not found") : Ok(map);
    }

    [HttpGet("patients/{patientId:int}/export")]
    public async Task<IActionResult> ExportJson(int patientId)
    {
        var bundle = await _gdprService.BuildExportBundleAsync(patientId);
        if (bundle == null)
        {
            return NotFound("Patient not found");
        }

        var json = JsonSerializer.Serialize(bundle.Export, ExportJsonOptions);
        return File(
            Encoding.UTF8.GetBytes(json),
            "application/json; charset=utf-8",
            $"gdpr-patient-{patientId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
    }

    [HttpGet("patients/{patientId:int}/export/zip")]
    public async Task<IActionResult> ExportZip(int patientId)
    {
        var bundle = await _gdprService.BuildExportBundleAsync(patientId);
        if (bundle == null)
        {
            return NotFound("Patient not found");
        }

        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var jsonEntry = archive.CreateEntry("patient.json", CompressionLevel.Optimal);
            await using (var writer = new StreamWriter(jsonEntry.Open(), new UTF8Encoding(false)))
            {
                await writer.WriteAsync(JsonSerializer.Serialize(bundle.Export, ExportJsonOptions));
            }

            foreach (var file in bundle.Files)
            {
                var entry = archive.CreateEntry(file.FileName, CompressionLevel.Optimal);
                await using var stream = entry.Open();
                await stream.WriteAsync(file.Data);
            }
        }

        return File(
            memory.ToArray(),
            "application/zip",
            $"gdpr-patient-{patientId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
    }

    [HttpPost("patients/{patientId:int}/anonymize")]
    public async Task<IActionResult> Anonymize(int patientId, [FromBody] AnonymizePatientRequest request)
    {
        var employeeId = _actorCountryContextService.GetActorEmployeeId();
        var (ok, error, result) = await _gdprService.AnonymizeAsync(
            patientId, request.Confirmation ?? string.Empty, employeeId);

        if (!ok)
        {
            return error == "Patient not found" ? NotFound(error) : BadRequest(error);
        }

        return Ok(result);
    }

    [HttpGet("patients/{patientId:int}/consents")]
    public async Task<ActionResult<List<PatientConsentDto>>> GetConsents(int patientId)
    {
        var exists = await _context.Patients.AnyAsync(patient => patient.Id == patientId);
        if (!exists)
        {
            return NotFound("Patient not found");
        }

        var consents = await _context.PatientConsents
            .AsNoTracking()
            .Where(consent => consent.PatientId == patientId)
            .OrderByDescending(consent => consent.AcceptedAtUtc)
            .Select(consent => new PatientConsentDto
            {
                Id = consent.Id,
                ConsentType = consent.ConsentType,
                Version = consent.Version,
                Language = consent.Language,
                Source = consent.Source,
                AcceptedAtUtc = consent.AcceptedAtUtc,
                RevokedAtUtc = consent.RevokedAtUtc,
                Notes = consent.Notes
            })
            .ToListAsync();

        return Ok(consents);
    }

    [HttpPost("patients/{patientId:int}/consents")]
    public async Task<ActionResult<PatientConsentDto>> RecordConsent(
        int patientId, [FromBody] RecordConsentRequest request)
    {
        var exists = await _context.Patients.AnyAsync(patient => patient.Id == patientId);
        if (!exists)
        {
            return NotFound("Patient not found");
        }

        if (string.IsNullOrWhiteSpace(request.ConsentType) || request.ConsentType.Trim().Length > 40)
        {
            return BadRequest("Consent type is required");
        }
        if (string.IsNullOrWhiteSpace(request.Version) || request.Version.Trim().Length > 20)
        {
            return BadRequest("Version is required");
        }

        var consent = new PatientConsent
        {
            PatientId = patientId,
            ConsentType = request.ConsentType.Trim().ToLowerInvariant(),
            Version = request.Version.Trim(),
            Language = NormalizeLanguage(request.Language),
            Source = string.IsNullOrWhiteSpace(request.Source) ? "Paper" : request.Source.Trim(),
            AcceptedAtUtc = request.AcceptedAtUtc ?? DateTime.UtcNow,
            Notes = Truncate(request.Notes, 500)
        };
        _context.PatientConsents.Add(consent);
        await _context.SaveChangesAsync();

        return Ok(new PatientConsentDto
        {
            Id = consent.Id,
            ConsentType = consent.ConsentType,
            Version = consent.Version,
            Language = consent.Language,
            Source = consent.Source,
            AcceptedAtUtc = consent.AcceptedAtUtc,
            RevokedAtUtc = consent.RevokedAtUtc,
            Notes = consent.Notes
        });
    }

    [HttpGet("consent-templates")]
    public async Task<ActionResult<List<ConsentTemplateDto>>> GetTemplates(
        [FromQuery] string? consentType = null,
        [FromQuery] string? language = null)
    {
        var query = _context.ConsentTemplates.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(consentType))
        {
            var type = consentType.Trim().ToLowerInvariant();
            query = query.Where(template => template.ConsentType == type);
        }
        if (!string.IsNullOrWhiteSpace(language))
        {
            var lang = NormalizeLanguage(language);
            query = query.Where(template => template.Language == lang);
        }

        var templates = await query
            .OrderBy(template => template.ConsentType)
            .ThenBy(template => template.Language)
            .ThenByDescending(template => template.CreatedAtUtc)
            .ToListAsync();

        return Ok(templates.Select(ToDto).ToList());
    }

    [HttpPost("consent-templates")]
    public async Task<ActionResult<ConsentTemplateDto>> CreateTemplate(
        [FromBody] SaveConsentTemplateRequest request)
    {
        var validation = ValidateTemplate(request);
        if (validation != null)
        {
            return BadRequest(validation);
        }

        var template = new ConsentTemplate
        {
            ConsentType = request.ConsentType.Trim().ToLowerInvariant(),
            Version = request.Version.Trim(),
            Language = NormalizeLanguage(request.Language),
            Title = request.Title.Trim(),
            Text = request.Text,
            IsActive = request.IsActive
        };

        var duplicate = await _context.ConsentTemplates.AnyAsync(existing =>
            existing.ConsentType == template.ConsentType &&
            existing.Version == template.Version &&
            existing.Language == template.Language);
        if (duplicate)
        {
            return Conflict("This consent version already exists for the language");
        }

        _context.ConsentTemplates.Add(template);
        await _context.SaveChangesAsync();
        return Ok(ToDto(template));
    }

    [HttpPut("consent-templates/{id:int}")]
    public async Task<ActionResult<ConsentTemplateDto>> UpdateTemplate(
        int id, [FromBody] SaveConsentTemplateRequest request)
    {
        var template = await _context.ConsentTemplates.FirstOrDefaultAsync(entry => entry.Id == id);
        if (template == null)
        {
            return NotFound("Consent template not found");
        }

        var validation = ValidateTemplate(request);
        if (validation != null)
        {
            return BadRequest(validation);
        }

        var consentType = request.ConsentType.Trim().ToLowerInvariant();
        var version = request.Version.Trim();
        var language = NormalizeLanguage(request.Language);

        var duplicate = await _context.ConsentTemplates.AnyAsync(existing =>
            existing.Id != id &&
            existing.ConsentType == consentType &&
            existing.Version == version &&
            existing.Language == language);
        if (duplicate)
        {
            return Conflict("This consent version already exists for the language");
        }

        template.ConsentType = consentType;
        template.Version = version;
        template.Language = language;
        template.Title = request.Title.Trim();
        template.Text = request.Text;
        template.IsActive = request.IsActive;
        template.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(ToDto(template));
    }

    [HttpDelete("consent-templates/{id:int}")]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        var template = await _context.ConsentTemplates.FirstOrDefaultAsync(entry => entry.Id == id);
        if (template == null)
        {
            return NotFound("Consent template not found");
        }

        _context.ConsentTemplates.Remove(template);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("requests")]
    public async Task<ActionResult<List<GdprRequest>>> GetRequests([FromQuery] int? patientId = null)
    {
        var query = _context.GdprRequests.AsNoTracking().AsQueryable();
        if (patientId.HasValue)
        {
            query = query.Where(request => request.PatientId == patientId.Value);
        }

        return Ok(await query
            .OrderByDescending(request => request.RequestedAtUtc)
            .Take(200)
            .ToListAsync());
    }

    private static string? ValidateTemplate(SaveConsentTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConsentType) || request.ConsentType.Trim().Length > 40)
        {
            return "Consent type is required";
        }
        if (string.IsNullOrWhiteSpace(request.Version) || request.Version.Trim().Length > 20)
        {
            return "Version is required";
        }
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200)
        {
            return "Title is required (max 200 characters)";
        }
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 20000)
        {
            return "Text is required (max 20000 characters)";
        }
        if (NormalizeLanguage(request.Language).Length is < 2 or > 5)
        {
            return "Language must be 2-5 characters";
        }
        return null;
    }

    private static string NormalizeLanguage(string? language) =>
        string.IsNullOrWhiteSpace(language) ? "cs" : language.Trim().ToLowerInvariant();

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    private static ConsentTemplateDto ToDto(ConsentTemplate template) => new()
    {
        Id = template.Id,
        ConsentType = template.ConsentType,
        Version = template.Version,
        Language = template.Language,
        Title = template.Title,
        Text = template.Text,
        IsActive = template.IsActive,
        UpdatedAtUtc = template.UpdatedAtUtc
    };
}
