using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HospitalsController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly ILogger<HospitalsController> _logger;

    public HospitalsController(DatabaseContext context, ILogger<HospitalsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<Hospital>>> GetAll()
    {
        try
        {
            var hospitals = await _context.Hospitals
                .Where(h => h.Active == true)
                .OrderBy(h => h.Address)
                .ToListAsync();

            return Ok(hospitals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hospitals");
            return StatusCode(500, new { Error = "Error getting hospitals", Details = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Hospital>> GetById(int id)
    {
        try
        {
            var hospital = await _context.Hospitals.FindAsync(id);
            if (hospital == null)
                return NotFound("Hospital not found");

            return Ok(hospital);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hospital");
            return StatusCode(500, new { Error = "Error getting hospital", Details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<Hospital>> Create([FromBody] CreateHospitalRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Address))
                return BadRequest("Hospital address is required");

            var hospital = new Hospital
            {
                Address = request.Address,
                Active = true
            };

            _context.Hospitals.Add(hospital);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = hospital.Id }, hospital);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating hospital");
            return StatusCode(500, new { Error = "Error creating hospital", Details = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHospitalRequest request)
    {
        try
        {
            var hospital = await _context.Hospitals.FindAsync(id);
            if (hospital == null)
                return NotFound("Hospital not found");

            if (!string.IsNullOrWhiteSpace(request.Address))
                hospital.Address = request.Address;

            if (request.Active.HasValue)
                hospital.Active = request.Active;

            await _context.SaveChangesAsync();
            return Ok(hospital);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating hospital");
            return StatusCode(500, new { Error = "Error updating hospital", Details = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var hospital = await _context.Hospitals.FindAsync(id);
            if (hospital == null)
                return NotFound("Hospital not found");

            hospital.Active = false;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Hospital deactivated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting hospital");
            return StatusCode(500, new { Error = "Error deleting hospital", Details = ex.Message });
        }
    }
}

public class CreateHospitalRequest
{
    public string Address { get; set; } = string.Empty;
}

public class UpdateHospitalRequest
{
    public string? Address { get; set; }
    public bool? Active { get; set; }
}
