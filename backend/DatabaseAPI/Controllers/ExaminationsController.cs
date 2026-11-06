using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.Services;
using DatabaseAPI.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExaminationsController : ControllerBase
{
    private readonly IExaminationsService _examinationsService;
    private readonly DatabaseContext _context;
    private readonly IEmployeeScopeService _employeeScopeService;
    private readonly ILogger<ExaminationsController> _logger;

    public ExaminationsController(
        IExaminationsService examinationsService,
        DatabaseContext context,
        IEmployeeScopeService employeeScopeService,
        ILogger<ExaminationsController> logger)
    {
        _examinationsService = examinationsService;
        _context = context;
        _employeeScopeService = employeeScopeService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ExaminationSearchResponse>> GetAll([FromQuery] ExaminationSearchFilters filters, CancellationToken cancellationToken)
    {
        try
        {
            await ApplyScopeAsync(filters, cancellationToken);
            var result = await _examinationsService.SearchAsync(filters, cancellationToken);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting examinations");
            return StatusCode(500, new { Error = "Error getting examinations" });
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] ExaminationSearchFilters filters, CancellationToken cancellationToken)
    {
        try
        {
            await ApplyScopeAsync(filters, cancellationToken);
            var result = await _examinationsService.ExportAsync(filters, cancellationToken);
            return File(result.Content, "application/zip", result.FileName);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting examinations");
            return StatusCode(500, new { Error = "Error exporting examinations" });
        }
    }

    private async Task ApplyScopeAsync(ExaminationSearchFilters filters, CancellationToken cancellationToken)
    {
        var actor = await _employeeScopeService.GetActorScopeAsync(cancellationToken);
        if (!actor.IsScopedAdmin)
        {
            filters.VisiblePatientIds = null;
            return;
        }

        filters.VisiblePatientIds = await PatientScope
            .Apply(_context.Patients, actor)
            .Select(patient => patient.Id)
            .ToListAsync(cancellationToken);
    }
}
