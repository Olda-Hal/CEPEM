using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using DatabaseAPI.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers;

/// <summary>
/// Measurements overview: every visit that carries a MEIK .tdb file, the
/// linked patient and the evaluation state, with a one-click way to close a
/// visit as evaluated (same effect as two doctor reviews).
/// Permission codes are enforced by the healthcare proxy.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MeasurementsController : ControllerBase
{
    private const int RequiredReviews = 2;

    private readonly DatabaseContext _context;
    private readonly IEmployeeScopeService _employeeScopeService;
    private readonly IActorCountryContextService _actorCountryContextService;
    private readonly ILogger<MeasurementsController> _logger;

    public MeasurementsController(
        DatabaseContext context,
        IEmployeeScopeService employeeScopeService,
        IActorCountryContextService actorCountryContextService,
        ILogger<MeasurementsController> logger)
    {
        _context = context;
        _employeeScopeService = employeeScopeService;
        _actorCountryContextService = actorCountryContextService;
        _logger = logger;
    }

    /// <summary>
    /// Visits with at least one .tdb document. <paramref name="evaluated"/>
    /// accepts all/yes/no/partial.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<MeasurementListDto>> GetMeasurements(
        [FromQuery] string evaluated = "all",
        [FromQuery] string search = "",
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(0, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.Visits
            .AsNoTracking()
            .Where(v => !v.Patient.IsDeleted)
            .Where(v => v.Documents.Any(d =>
                d.PatientDocument.OriginalFileName.ToLower().EndsWith(".tdb")));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(v =>
                v.Patient.Person.FirstName.ToLower().Contains(term) ||
                v.Patient.Person.LastName.ToLower().Contains(term) ||
                v.Patient.Person.UID.ToLower().Contains(term) ||
                v.Documents.Any(d => d.PatientDocument.OriginalFileName.ToLower().Contains(term)));
        }

        query = await ApplyScopeAsync(query);

        var rows = await query
            .OrderByDescending(v => v.VisitDate)
            .ThenByDescending(v => v.Id)
            .Select(v => new
            {
                VisitId = v.Id,
                PatientId = v.PatientId,
                FirstName = v.Patient.Person.FirstName,
                LastName = v.Patient.Person.LastName,
                v.Patient.Person.UID,
                v.VisitDate,
                Documents = v.Documents
                    .Where(d => d.PatientDocument.OriginalFileName.ToLower().EndsWith(".tdb"))
                    .OrderBy(d => d.SortOrder)
                    .ThenBy(d => d.Id)
                    .Select(d => new MeasurementDocumentDto
                    {
                        DocumentId = d.PatientDocumentId,
                        OriginalFileName = d.PatientDocument.OriginalFileName,
                        UploadedAt = d.PatientDocument.UploadedAt,
                        FileSize = d.PatientDocument.FileSize,
                        EvaluationCaseId = _context.EvaluationCases
                            .Where(c => c.PatientDocumentId == d.PatientDocumentId)
                            .Select(c => (int?)c.Id)
                            .FirstOrDefault()
                    })
                    .ToList(),
                Cases = _context.EvaluationCases
                    .Where(c => c.VisitId == v.Id && c.ImageKey == "tdb")
                    .Select(c => new
                    {
                        c.Id,
                        c.EvaluatedAtUtc,
                        ReviewCount = c.Reviews.Count()
                    })
                    .ToList()
            })
            .ToListAsync();

        var items = rows.Select(row =>
        {
            var evaluatedCount = row.Cases.Count(c =>
                c.ReviewCount >= RequiredReviews || c.EvaluatedAtUtc != null);
            var partiallyReviewed = row.Cases.Any(c => c.ReviewCount > 0);
            var allEvaluated = row.Cases.Count > 0 && evaluatedCount == row.Cases.Count;

            var status = allEvaluated
                ? "evaluated"
                : partiallyReviewed || evaluatedCount > 0
                    ? "partial"
                    : "pending";

            return new MeasurementVisitDto
            {
                VisitId = row.VisitId,
                PatientId = row.PatientId,
                PatientName = $"{row.LastName}, {row.FirstName}".Trim(' ', ','),
                PatientUid = row.UID,
                VisitDate = row.VisitDate,
                Status = status,
                EvaluationCaseCount = row.Cases.Count,
                EvaluatedCaseCount = evaluatedCount,
                IsManuallyEvaluated = row.Cases.Count > 0 &&
                    row.Cases.All(c => c.ReviewCount == 0 && c.EvaluatedAtUtc != null),
                LastEvaluatedAtUtc = row.Cases
                    .Select(c => c.EvaluatedAtUtc)
                    .Where(value => value != null)
                    .Max(),
                Documents = row.Documents
            };
        }).ToList();

        items = evaluated.Trim().ToLowerInvariant() switch
        {
            "yes" or "true" => items.Where(item => item.Status == "evaluated").ToList(),
            "no" or "false" => items.Where(item => item.Status != "evaluated").ToList(),
            "partial" => items.Where(item => item.Status == "partial").ToList(),
            _ => items
        };

        return Ok(new MeasurementListDto
        {
            Total = items.Count,
            Items = items.Skip(page * pageSize).Take(pageSize).ToList()
        });
    }

    /// <summary>
    /// Marks every .tdb case of the visit as evaluated. From that moment the
    /// visit behaves like a measurement finished by two doctors: it leaves the
    /// Vyhodnocování queue and its report is available in the visit.
    /// </summary>
    [HttpPost("visits/{visitId}/mark-evaluated")]
    public async Task<IActionResult> MarkVisitEvaluated(int visitId)
    {
        var actorId = _actorCountryContextService.GetActorEmployeeId();
        if (actorId == null)
        {
            return Unauthorized();
        }

        var visit = await _context.Visits
            .Include(v => v.Patient)
            .FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit == null)
        {
            return NotFound("Visit not found");
        }

        if (!await CanAccessPatientAsync(visit.Patient))
        {
            return StatusCode(403, "K této návštěvě nemáte přístup.");
        }

        var cases = await _context.EvaluationCases
            .Where(c => c.VisitId == visitId && c.ImageKey == "tdb")
            .ToListAsync();

        var now = DateTime.UtcNow;
        var marked = 0;
        foreach (var evaluationCase in cases)
        {
            var reviews = await _context.EvaluationReviews
                .CountAsync(r => r.EvaluationCaseId == evaluationCase.Id);
            if (evaluationCase.EvaluatedAtUtc != null || reviews >= RequiredReviews)
            {
                continue;
            }

            evaluationCase.EvaluatedAtUtc = now;
            evaluationCase.EvaluatedByEmployeeId = actorId;
            marked++;
        }

        if (marked > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "Visit {VisitId} marked as evaluated by employee {EmployeeId} ({Count} measurement(s))",
                visitId, actorId, marked);
        }

        return Ok(new { visitId, marked });
    }

    private async Task<IQueryable<Visit>> ApplyScopeAsync(IQueryable<Visit> query)
    {
        var actor = await _employeeScopeService.GetActorScopeAsync();
        if (actor.HasFullAccess || !actor.IsScopedAdmin)
        {
            return query;
        }

        if (actor.IsCountryAdmin)
        {
            var countryCodes = actor.AdministeredCountryScopeIds
                .Select(CountryScopeMapper.ToCountryCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!)
                .ToList();
            return query.Where(v => v.Patient.CountryCode != null &&
                                    countryCodes.Contains(v.Patient.CountryCode));
        }

        // Center Admin sees only patients whose home center they administer.
        return query.Where(v => v.Patient.HomeHospitalId != null &&
                                actor.HospitalIds.Contains(v.Patient.HomeHospitalId.Value));
    }

    private async Task<bool> CanAccessPatientAsync(Patient? patient)
    {
        if (patient == null)
        {
            return false;
        }

        var actor = await _employeeScopeService.GetActorScopeAsync();
        if (actor.HasFullAccess || !actor.IsScopedAdmin)
        {
            return true;
        }

        if (actor.IsCountryAdmin)
        {
            return actor.AdministeredCountryScopeIds.Contains(
                CountryScopeMapper.ToScopeId(patient.CountryCode));
        }

        return patient.HomeHospitalId != null && actor.HospitalIds.Contains(patient.HomeHospitalId.Value);
    }
}
