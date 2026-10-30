using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;

namespace DatabaseAPI.Controllers;

/// <summary>
/// Vyhodnocování: measurements wait for a doctor; each measurement can be
/// evaluated independently by two different doctors. Authorization
/// (evaluations.read/write) is enforced by the healthcare proxy; actor
/// identity arrives via X-Actor-* headers.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EvaluationsController : ControllerBase
{
    /// <summary>Two independent reviews per measurement, by different doctors.</summary>
    private const int MaxReviews = 2;

    private readonly DatabaseContext _context;
    private readonly IActorCountryContextService _actorCountryContextService;
    private readonly IEmployeeScopeService _employeeScopeService;
    private readonly ILogger<EvaluationsController> _logger;

    public EvaluationsController(
        DatabaseContext context,
        IActorCountryContextService actorCountryContextService,
        IEmployeeScopeService employeeScopeService,
        ILogger<EvaluationsController> logger)
    {
        _context = context;
        _actorCountryContextService = actorCountryContextService;
        _employeeScopeService = employeeScopeService;
        _logger = logger;
    }

    /// <summary>
    /// The next measurement for the signed-in doctor: the oldest one with no
    /// review yet; when none are left, the oldest one with a single review.
    /// A doctor never receives a measurement they already reviewed.
    /// </summary>
    [HttpGet("next")]
    public async Task<ActionResult<EvaluationCaseDto>> GetNext()
    {
        var actorId = _actorCountryContextService.GetActorEmployeeId();
        if (actorId == null)
        {
            return Unauthorized();
        }

        var cases = await _context.EvaluationCases
            .Include(c => c.Patient)
                .ThenInclude(p => p!.Person)
            .Include(c => c.Reviews)
            .Where(c => c.ImageKey == "tdb")
            .ToListAsync();

        var actor = await _employeeScopeService.GetActorScopeAsync();
        IEnumerable<EvaluationCase> candidates = cases
            .Where(c => c.Reviews.Count < MaxReviews)
            .Where(c => c.Reviews.All(r => r.EmployeeId != actorId));

        if (actor.IsCountryAdmin && !actor.HasFullAccess)
        {
            candidates = candidates.Where(c => c.Patient == null ||
                actor.AdministeredCountryScopeIds.Contains(
                    DatabaseAPI.Utils.CountryScopeMapper.ToScopeId(c.Patient.CountryCode)));
        }

        var next = candidates
            .OrderBy(c => c.Reviews.Count)      // unreviewed first…
            .ThenBy(c => c.CreatedAtUtc)        // …oldest first
            .FirstOrDefault();

        if (next == null)
        {
            return NoContent();
        }

        // A re-evaluation must not reveal the first doctor's findings.
        return Ok(ToDto(next, includeSlices: true, includeReviews: false));
    }

    [HttpGet("cases")]
    public async Task<ActionResult<List<EvaluationCaseDto>>> GetCases([FromQuery] bool includeEvaluated = false)
    {
        var query = _context.EvaluationCases
            .Include(c => c.Patient)
                .ThenInclude(p => p!.Person)
            .Include(c => c.Reviews)
            .AsQueryable();

        if (!includeEvaluated)
        {
            query = query.Where(c => !c.Reviews.Any());
        }

        var actor = await _employeeScopeService.GetActorScopeAsync();
        var cases = await query
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync();

        // A Country Admin only sees cases for patients in administered
        // countries; doctors and full admins see the whole queue.
        if (actor.IsCountryAdmin && !actor.HasFullAccess)
        {
            cases = cases
                .Where(c => c.Patient == null ||
                    actor.AdministeredCountryScopeIds.Contains(
                        DatabaseAPI.Utils.CountryScopeMapper.ToScopeId(c.Patient.CountryCode)))
                .ToList();
        }

        return Ok(cases.Select(c => ToDto(c, includeSlices: false, includeReviews: false)).ToList());
    }

    [HttpGet("cases/{id}")]
    public async Task<ActionResult<EvaluationCaseDto>> GetCase(int id)
    {
        var evaluationCase = await _context.EvaluationCases
            .Include(c => c.Patient)
                .ThenInclude(p => p!.Person)
            .Include(c => c.Reviews)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (evaluationCase == null)
        {
            return NotFound("Evaluation case not found");
        }

        if (!await CanAccessCaseAsync(evaluationCase))
        {
            return StatusCode(403, "K tomuto případu nemáte přístup.");
        }

        return Ok(ToDto(evaluationCase, includeSlices: true, includeReviews: false));
    }

    /// <summary>
    /// Interactive report stored with the visit: the measurement plus every
    /// doctor's markers and comments.
    /// </summary>
    [HttpGet("cases/{id}/report")]
    public async Task<ActionResult<EvaluationCaseDto>> GetReport(int id)
    {
        var evaluationCase = await _context.EvaluationCases
            .Include(c => c.Patient)
                .ThenInclude(p => p!.Person)
            .Include(c => c.Reviews)
                .ThenInclude(r => r.Employee!)
                    .ThenInclude(e => e.Person)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (evaluationCase == null ||
            evaluationCase.VisitId == null ||
            evaluationCase.Reviews.Count == 0)
        {
            return NotFound("Vyhodnocení není dostupné.");
        }

        return Ok(ToDto(evaluationCase, includeSlices: true, includeReviews: true));
    }

    public class SaveEvaluationRequest
    {
        /// <summary>JSON array of [{ slice, x, y, note }] findings.</summary>
        public string AnnotationsJson { get; set; } = "[]";
        public string? Recommendation { get; set; }

        /// <summary>"OK" (no findings) or "Findings" for TDB measurements.</summary>
        public string? Outcome { get; set; }
    }

    [HttpPost("cases/{id}/evaluate")]
    public async Task<ActionResult<EvaluationCaseDto>> Evaluate(int id, [FromBody] SaveEvaluationRequest request)
    {
        var actorId = _actorCountryContextService.GetActorEmployeeId();
        if (actorId == null)
        {
            return Unauthorized();
        }

        // Serializable so two doctors cannot both take the second review slot.
        await using var transaction = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        var evaluationCase = await _context.EvaluationCases
            .Include(c => c.Patient)
                .ThenInclude(p => p!.Person)
            .Include(c => c.Reviews)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (evaluationCase == null)
        {
            return NotFound("Evaluation case not found");
        }

        if (!await CanAccessCaseAsync(evaluationCase))
        {
            return StatusCode(403, "K tomuto případu nemáte přístup.");
        }

        var isTdb = string.Equals(evaluationCase.ImageKey, "tdb", StringComparison.OrdinalIgnoreCase);
        var outcome = request.Outcome?.Trim();
        if (!string.IsNullOrEmpty(outcome) && outcome != "OK" && outcome != "Findings")
        {
            return BadRequest("Neplatný výsledek vyšetření.");
        }

        if (isTdb && string.IsNullOrEmpty(outcome))
        {
            return BadRequest("Chybí výsledek vyšetření.");
        }

        if (evaluationCase.Reviews.Count >= MaxReviews)
        {
            return Conflict("Měření už vyhodnotili dva lékaři.");
        }

        if (evaluationCase.Reviews.Any(r => r.EmployeeId == actorId))
        {
            return Conflict("Toto měření jste už hodnotil.");
        }

        var now = DateTime.UtcNow;
        var review = new EvaluationReview
        {
            EvaluationCaseId = evaluationCase.Id,
            EmployeeId = actorId,
            AnnotationsJson = request.AnnotationsJson,
            Recommendation = request.Recommendation,
            Outcome = string.IsNullOrEmpty(outcome) ? null : outcome,
            EvaluatedAtUtc = now,
            CreatedAtUtc = now
        };
        evaluationCase.Reviews.Add(review);
        _context.EvaluationReviews.Add(review);

        // Keep the legacy single-evaluation columns for old clients only;
        // the TDB workflow reads reviews exclusively.
        if (!isTdb)
        {
            evaluationCase.AnnotationsJson = request.AnnotationsJson;
            evaluationCase.Recommendation = request.Recommendation;
            evaluationCase.Outcome = string.IsNullOrEmpty(outcome) ? null : outcome;
            evaluationCase.EvaluatedByEmployeeId = actorId;
            evaluationCase.EvaluatedAtUtc = now;
        }

        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // Unique index (EvaluationCaseId, EmployeeId) lost a concurrent race.
            return Conflict("Toto měření jste už hodnotil.");
        }

        _logger.LogInformation(
            "Evaluation case {CaseId} reviewed by employee {EmployeeId} (review {ReviewNumber}/{MaxReviews})",
            id, actorId, evaluationCase.Reviews.Count + 1, MaxReviews);

        return Ok(ToDto(evaluationCase, includeSlices: true, includeReviews: false));
    }

    /// <summary>
    /// Country Admin may only touch cases whose patient belongs to an
    /// administered country. Other callers are gated by role permissions.
    /// </summary>
    private async Task<bool> CanAccessCaseAsync(EvaluationCase evaluationCase)
    {
        var actor = await _employeeScopeService.GetActorScopeAsync();
        if (!actor.IsCountryAdmin || actor.HasFullAccess || evaluationCase.Patient == null)
        {
            return true;
        }

        return actor.AdministeredCountryScopeIds.Contains(
            DatabaseAPI.Utils.CountryScopeMapper.ToScopeId(evaluationCase.Patient.CountryCode));
    }

    private static EvaluationCaseDto ToDto(EvaluationCase c, bool includeSlices, bool includeReviews)
    {
        var isTdb = string.Equals(c.ImageKey, "tdb", StringComparison.OrdinalIgnoreCase);
        var reviews = c.Reviews.OrderBy(r => r.EvaluatedAtUtc).ToList();
        var latestReview = reviews.Count > 0 ? reviews[^1] : null;

        // TDB measurements are presented anonymously to the evaluator.
        var patientName = !isTdb && c.Patient?.Person is { } person
            ? $"{person.FirstName} {person.LastName}"
            : null;

        return new EvaluationCaseDto
        {
            Id = c.Id,
            // The linked patient stays in the database only; the evaluating
            // doctor must not receive any patient identity for a TDB case.
            PatientId = isTdb ? null : c.PatientId,
            PatientName = patientName,
            Title = c.Title,
            ImageKey = c.ImageKey,
            // For TDB the case-level legacy fields stay hidden — they may
            // hold the first evaluator's findings.
            AnnotationsJson = isTdb ? null : c.AnnotationsJson,
            Recommendation = isTdb ? null : c.Recommendation,
            Outcome = isTdb ? null : c.Outcome,
            Side = c.SourceSide,
            Posture = c.SourcePosture,
            Conditions = c.SourceConditions,
            Tdb = includeSlices && isTdb ? DeserializeSlices(c.SlicesJson) : null,
            EvaluatedAtUtc = isTdb ? latestReview?.EvaluatedAtUtc : c.EvaluatedAtUtc,
            IsEvaluated = isTdb ? reviews.Count > 0 : c.EvaluatedAtUtc != null,
            ReviewCount = reviews.Count,
            MaxReviews = MaxReviews,
            Reviews = includeReviews
                ? reviews.Select(ToReviewDto).ToList()
                : null
        };
    }

    private static EvaluationReviewDto ToReviewDto(EvaluationReview review)
    {
        var doctorName = review.Employee?.Person is { } person
            ? $"{person.TitleBefore} {person.FirstName} {person.LastName} {person.TitleAfter}".Trim()
            : null;

        return new EvaluationReviewDto
        {
            Id = review.Id,
            DoctorName = doctorName,
            AnnotationsJson = review.AnnotationsJson,
            Recommendation = review.Recommendation,
            Outcome = review.Outcome,
            EvaluatedAtUtc = review.EvaluatedAtUtc
        };
    }

    private static TdbSlicesDto? DeserializeSlices(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TdbSlicesDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public class EvaluationCaseDto
    {
        public int Id { get; set; }
        public int? PatientId { get; set; }
        public string? PatientName { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageKey { get; set; } = string.Empty;
        public string? AnnotationsJson { get; set; }
        public string? Recommendation { get; set; }
        public string? Outcome { get; set; }
        public string? Side { get; set; }
        public string? Posture { get; set; }
        public string? Conditions { get; set; }
        public TdbSlicesDto? Tdb { get; set; }
        public DateTime? EvaluatedAtUtc { get; set; }
        public bool IsEvaluated { get; set; }
        public int ReviewCount { get; set; }
        public int MaxReviews { get; set; }
        public List<EvaluationReviewDto>? Reviews { get; set; }
    }

    public class EvaluationReviewDto
    {
        public int Id { get; set; }
        public string? DoctorName { get; set; }
        public string? AnnotationsJson { get; set; }
        public string? Recommendation { get; set; }
        public string? Outcome { get; set; }
        public DateTime EvaluatedAtUtc { get; set; }
    }

    public class TdbSlicesDto
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public double WindowLow { get; set; }
        public double WindowHigh { get; set; }
        public double NoiseLevel { get; set; }
        public List<TdbSliceDto> Slices { get; set; } = new();
    }

    public class TdbSliceDto
    {
        public int Index { get; set; }
        public int DepthMm { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Mean { get; set; }
        /// <summary>Base64 of width*height little-endian float32 samples.</summary>
        public string Values { get; set; } = string.Empty;
        public TdbHistogramDto Histogram { get; set; } = new();
    }

    public class TdbHistogramDto
    {
        public List<int> Counts { get; set; } = new();
        public List<double> Edges { get; set; } = new();
    }
}
