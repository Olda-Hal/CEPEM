using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;

namespace DatabaseAPI.Controllers;

/// <summary>
/// Mock "Vyhodnocování" queue: open cases land here for the evaluating
/// doctor. Authorization (evaluations.read/write) is enforced by the
/// healthcare proxy; actor identity arrives via X-Actor-* headers.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EvaluationsController : ControllerBase
{
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

    [HttpGet("cases")]
    public async Task<ActionResult<List<EvaluationCaseDto>>> GetCases([FromQuery] bool includeEvaluated = false)
    {
        var query = _context.EvaluationCases
            .Include(c => c.Patient)
                .ThenInclude(p => p!.Person)
            .AsQueryable();

        if (!includeEvaluated)
        {
            query = query.Where(c => c.EvaluatedAtUtc == null);
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

        // The list stays light — slice payloads are only sent for the opened case.
        return Ok(cases.Select(c => ToDto(c, includeSlices: false)).ToList());
    }

    [HttpGet("cases/{id}")]
    public async Task<ActionResult<EvaluationCaseDto>> GetCase(int id)
    {
        var evaluationCase = await _context.EvaluationCases
            .Include(c => c.Patient)
                .ThenInclude(p => p!.Person)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (evaluationCase == null)
        {
            return NotFound("Evaluation case not found");
        }

        if (!await CanAccessCaseAsync(evaluationCase))
        {
            return StatusCode(403, "K tomuto případu nemáte přístup.");
        }

        return Ok(ToDto(evaluationCase, includeSlices: true));
    }

    public class SaveEvaluationRequest
    {
        /// <summary>JSON array of [{ x, y, note }] markers in normalized coords.</summary>
        public string AnnotationsJson { get; set; } = "[]";
        public string? Recommendation { get; set; }

        /// <summary>"OK" (no findings) or "Findings" for TDB measurements.</summary>
        public string? Outcome { get; set; }
    }

    [HttpPost("cases/{id}/evaluate")]
    public async Task<ActionResult<EvaluationCaseDto>> Evaluate(int id, [FromBody] SaveEvaluationRequest request)
    {
        var evaluationCase = await _context.EvaluationCases
            .Include(c => c.Patient)
                .ThenInclude(p => p!.Person)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (evaluationCase == null)
        {
            return NotFound("Evaluation case not found");
        }

        if (!await CanAccessCaseAsync(evaluationCase))
        {
            return StatusCode(403, "K tomuto případu nemáte přístup.");
        }

        var outcome = request.Outcome?.Trim();
        if (!string.IsNullOrEmpty(outcome) && outcome != "OK" && outcome != "Findings")
        {
            return BadRequest("Neplatný výsledek vyšetření.");
        }

        evaluationCase.AnnotationsJson = request.AnnotationsJson;
        evaluationCase.Recommendation = request.Recommendation;
        evaluationCase.Outcome = string.IsNullOrEmpty(outcome) ? null : outcome;
        evaluationCase.EvaluatedByEmployeeId = _actorCountryContextService.GetActorEmployeeId();
        evaluationCase.EvaluatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Evaluation case {CaseId} evaluated by employee {EmployeeId}", id, evaluationCase.EvaluatedByEmployeeId);
        return Ok(ToDto(evaluationCase, includeSlices: true));
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

    private static EvaluationCaseDto ToDto(EvaluationCase c, bool includeSlices)
    {
        var isTdb = string.Equals(c.ImageKey, "tdb", StringComparison.OrdinalIgnoreCase);

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
            AnnotationsJson = c.AnnotationsJson,
            Recommendation = c.Recommendation,
            Outcome = c.Outcome,
            Side = c.SourceSide,
            Posture = c.SourcePosture,
            Conditions = c.SourceConditions,
            Tdb = includeSlices && isTdb ? DeserializeSlices(c.SlicesJson) : null,
            EvaluatedAtUtc = c.EvaluatedAtUtc,
            IsEvaluated = c.EvaluatedAtUtc != null
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
