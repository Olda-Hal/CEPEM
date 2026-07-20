using DatabaseAPI.APIModels;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExaminationsController : ControllerBase
{
    private readonly IExaminationsService _examinationsService;
    private readonly ILogger<ExaminationsController> _logger;

    public ExaminationsController(IExaminationsService examinationsService, ILogger<ExaminationsController> logger)
    {
        _examinationsService = examinationsService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ExaminationSearchResponse>> GetAll([FromQuery] ExaminationSearchFilters filters, CancellationToken cancellationToken)
    {
        try
        {
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
}
