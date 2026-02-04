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
    public async Task<ActionResult<List<ExaminationType>>> GetAll()
    {
        try
        {
            var types = await _context.ExaminationTypes.OrderBy(e => e.Name).ToListAsync();
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
            var type = await _context.ExaminationTypes.FindAsync(id);
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
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Examination type name is required");

            var existing = await _context.ExaminationTypes.FirstOrDefaultAsync(e => e.Name == request.Name);
            if (existing != null)
                return BadRequest("Examination type with this name already exists");

            var examinationType = new ExaminationType
            {
                Name = request.Name
            };

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
            var type = await _context.ExaminationTypes.FindAsync(id);
            if (type == null)
                return NotFound("Examination type not found");

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Examination type name is required");

            var existing = await _context.ExaminationTypes.FirstOrDefaultAsync(e => e.Name == request.Name && e.Id != id);
            if (existing != null)
                return BadRequest("Examination type with this name already exists");

            type.Name = request.Name;
            _context.ExaminationTypes.Update(type);
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
    public string Name { get; set; } = string.Empty;
}

public class UpdateExaminationTypeRequest
{
    public string Name { get; set; } = string.Empty;
}
