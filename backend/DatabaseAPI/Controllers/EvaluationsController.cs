using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        return Ok(cases.Select(ToDto).ToList());
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

        return Ok(ToDto(evaluationCase));
    }

    public class SaveEvaluationRequest
    {
        /// <summary>JSON array of [{ x, y, note }] markers in normalized coords.</summary>
        public string AnnotationsJson { get; set; } = "[]";
        public string? Recommendation { get; set; }
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

        evaluationCase.AnnotationsJson = request.AnnotationsJson;
        evaluationCase.Recommendation = request.Recommendation;
        evaluationCase.EvaluatedByEmployeeId = _actorCountryContextService.GetActorEmployeeId();
        evaluationCase.EvaluatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Evaluation case {CaseId} evaluated by employee {EmployeeId}", id, evaluationCase.EvaluatedByEmployeeId);
        return Ok(ToDto(evaluationCase));
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

    private static EvaluationCaseDto ToDto(EvaluationCase c) => new()
    {
        Id = c.Id,
        PatientId = c.PatientId,
        PatientName = c.Patient?.Person is { } person ? $"{person.FirstName} {person.LastName}" : null,
        Title = c.Title,
        ImageKey = c.ImageKey,
        AnnotationsJson = c.AnnotationsJson,
        Recommendation = c.Recommendation,
        EvaluatedAtUtc = c.EvaluatedAtUtc,
        IsEvaluated = c.EvaluatedAtUtc != null
    };

    public class EvaluationCaseDto
    {
        public int Id { get; set; }
        public int? PatientId { get; set; }
        public string? PatientName { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageKey { get; set; } = string.Empty;
        public string? AnnotationsJson { get; set; }
        public string? Recommendation { get; set; }
        public DateTime? EvaluatedAtUtc { get; set; }
        public bool IsEvaluated { get; set; }
    }
}
