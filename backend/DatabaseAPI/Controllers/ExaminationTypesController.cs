using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExaminationTypesController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly ILogger<ExaminationTypesController> _logger;

    public ExaminationTypesController(DatabaseContext context, ILogger<ExaminationTypesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<object>>> GetAll([FromQuery] string? language = "cs")
    {
        try
        {
            var types = await _context.ExaminationTypes
                .Include(e => e.NameTranslation)
                .OrderBy(e => e.NameTranslation!.EN)
                .Select(e => new
                {
                    e.Id,
                    Name = language == "cs"
                        ? e.NameTranslation!.CS ?? e.NameTranslation.EN
                        : language == "nl"
                            ? e.NameTranslation!.NL ?? e.NameTranslation.EN
                            : e.NameTranslation!.EN
                })
                .ToListAsync();
            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting examination types");
            return StatusCode(500, new { Error = "Error getting examination types", Details = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ExaminationType>> GetById(int id)
    {
        try
        {
            var type = await _context.ExaminationTypes
                .Include(e => e.NameTranslation)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (type == null)
                return NotFound("Examination type not found");

            return Ok(type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting examination type");
            return StatusCode(500, new { Error = "Error getting examination type", Details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ExaminationType>> Create([FromBody] CreateExaminationTypeRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.EN))
                return BadRequest("Examination type EN name is required");

            var translation = new Translation { EN = request.EN, CS = request.CS, NL = request.NL };
            _context.Translations.Add(translation);
            await _context.SaveChangesAsync();

            var examinationType = new ExaminationType { NameTranslationId = translation.Id };
            _context.ExaminationTypes.Add(examinationType);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = examinationType.Id }, examinationType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating examination type");
            return StatusCode(500, new { Error = "Error creating examination type", Details = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExaminationTypeRequest request)
    {
        try
        {
            var type = await _context.ExaminationTypes
                .Include(e => e.NameTranslation)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (type == null)
                return NotFound("Examination type not found");

            if (string.IsNullOrWhiteSpace(request.EN))
                return BadRequest("Examination type EN name is required");

            if (type.NameTranslation == null)
            {
                var translation = new Translation { EN = request.EN, CS = request.CS, NL = request.NL };
                _context.Translations.Add(translation);
                await _context.SaveChangesAsync();
                type.NameTranslationId = translation.Id;
            }
            else
            {
                type.NameTranslation.EN = request.EN;
                type.NameTranslation.CS = request.CS;
                type.NameTranslation.NL = request.NL;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Examination type updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating examination type");
            return StatusCode(500, new { Error = "Error updating examination type", Details = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var type = await _context.ExaminationTypes.FindAsync(id);
            if (type == null)
                return NotFound("Examination type not found");

            var hasReservations = await _context.Reservations.AnyAsync(r => r.ExaminationTypeId == id);
            if (hasReservations)
                return BadRequest("Cannot delete examination type that has reservations");

            _context.ExaminationTypes.Remove(type);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Examination type deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting examination type");
            return StatusCode(500, new { Error = "Error deleting examination type", Details = ex.Message });
        }
    }
}

public class CreateExaminationTypeRequest
{
    public string EN { get; set; } = string.Empty;
    public string? CS { get; set; }
    public string? NL { get; set; }
}

public class UpdateExaminationTypeRequest
{
    public string EN { get; set; } = string.Empty;
    public string? CS { get; set; }
    public string? NL { get; set; }
}
