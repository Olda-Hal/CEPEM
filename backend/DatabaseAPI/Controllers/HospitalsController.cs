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
    public async Task<ActionResult<List<object>>> GetAll()
    {
        try
        {
            var hospitals = await _context.Hospitals
                .Include(h => h.Address)
                .Where(h => h.Active == true)
                .OrderBy(h => h.Name)
                .Select(h => new
                {
                    h.Id,
                    h.Name,
                    h.Active,
                    h.CompanyIco,
                    h.CompanyName,
                    h.ParentHospitalId,
                    Address = h.Address == null ? null : new
                    {
                        h.Address.Street,
                        h.Address.City,
                        h.Address.PostalCode,
                        h.Address.Country
                    }
                })
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
            var hospital = new Hospital
            {
                Name = request.Name,
                Active = true
            };

            if (request.Street != null || request.City != null || request.PostalCode != null || request.Country != null)
            {
                var address = new Address
                {
                    Street = request.Street ?? string.Empty,
                    City = request.City ?? string.Empty,
                    PostalCode = request.PostalCode ?? string.Empty,
                    Country = request.Country ?? string.Empty
                };
                _context.Addresses.Add(address);
                await _context.SaveChangesAsync();
                hospital.AddressId = address.Id;
            }

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
            var hospital = await _context.Hospitals
                .Include(h => h.Address)
                .FirstOrDefaultAsync(h => h.Id == id);
            if (hospital == null)
                return NotFound("Hospital not found");

            if (request.Name != null)
                hospital.Name = request.Name;

            if (request.Active.HasValue)
                hospital.Active = request.Active;

            if (request.Street != null || request.City != null || request.PostalCode != null || request.Country != null)
            {
                if (hospital.Address != null)
                {
                    if (request.Street != null) hospital.Address.Street = request.Street;
                    if (request.City != null) hospital.Address.City = request.City;
                    if (request.PostalCode != null) hospital.Address.PostalCode = request.PostalCode;
                    if (request.Country != null) hospital.Address.Country = request.Country;
                }
                else
                {
                    var address = new Address
                    {
                        Street = request.Street ?? string.Empty,
                        City = request.City ?? string.Empty,
                        PostalCode = request.PostalCode ?? string.Empty,
                        Country = request.Country ?? string.Empty
                    };
                    _context.Addresses.Add(address);
                    await _context.SaveChangesAsync();
                    hospital.AddressId = address.Id;
                }
            }

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
    public string? Name { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public class UpdateHospitalRequest
{
    public string? Name { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public bool? Active { get; set; }
}
