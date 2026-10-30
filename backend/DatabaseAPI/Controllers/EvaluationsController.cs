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
    private readonly ILogger<EvaluationsController> _logger;

    public EvaluationsController(DatabaseContext context, IActorCountryContextService actorCountryContextService, ILogger<EvaluationsController> logger)
    {
        _context = context;
        _actorCountryContextService = actorCountryContextService;
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

        var cases = await query
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => ToDto(c))
            .ToListAsync();

        return Ok(cases);
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

        evaluationCase.AnnotationsJson = request.AnnotationsJson;
        evaluationCase.Recommendation = request.Recommendation;
        evaluationCase.EvaluatedByEmployeeId = _actorCountryContextService.GetActorEmployeeId();
        evaluationCase.EvaluatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Evaluation case {CaseId} evaluated by employee {EmployeeId}", id, evaluationCase.EvaluatedByEmployeeId);
        return Ok(ToDto(evaluationCase));
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
