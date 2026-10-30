using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HospitalsController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly ILogger<HospitalsController> _logger;
    private readonly IEmployeeScopeService _employeeScopeService;

    public HospitalsController(
        DatabaseContext context,
        ILogger<HospitalsController> logger,
        IEmployeeScopeService employeeScopeService)
    {
        _context = context;
        _logger = logger;
        _employeeScopeService = employeeScopeService;
    }

    /// <summary>
    /// Country Admin may create/edit centers only within administered countries.
    /// Returns null when allowed; a 403 result otherwise.
    /// </summary>
    private async Task<IActionResult?> CheckCountryScopeAllowedAsync(int requestedScopeId)
    {
        var actor = await _employeeScopeService.GetActorScopeAsync();
        if (!actor.IsScopedAdmin || !actor.IsCountryAdmin)
        {
            return null; // full access or scoping handled elsewhere
        }

        return actor.AdministeredCountryScopeIds.Contains(requestedScopeId)
            ? null
            : StatusCode(403, "Centrum lze zakládat pouze v zemích, které spravujete");
    }

    [HttpGet]
    public async Task<ActionResult<List<object>>> GetAll([FromQuery] int? countryScopeId = null)
    {
        try
        {
            var query = _context.Hospitals
                .Include(h => h.Address)
                .Include(h => h.ContactToObjects)
                    .ThenInclude(cto => cto.Contact)
                        .ThenInclude(c => c.PhoneNumbers)
                .Where(h => h.Active == true)
                .AsQueryable();

            // Scoped admins only see centers within their remit: Center Admin
            // their assigned centers, Country Admin the administered countries.
            var actor = await _employeeScopeService.GetActorScopeAsync();
            if (actor.IsScopedAdmin)
            {
                if (actor.IsCenterAdmin && !actor.IsCountryAdmin)
                {
                    query = query.Where(h => actor.HospitalIds.Contains(h.Id));
                }
                else
                {
                    query = query.Where(h => actor.AdministeredCountryScopeIds.Contains(h.CountryScopeId));
                }
            }

            if (countryScopeId.HasValue && countryScopeId.Value > 0)
            {
                query = query.Where(h => h.CountryScopeId == countryScopeId.Value);
            }

            var hospitals = await query
                .OrderBy(h => h.Name)
                .Select(h => new
                {
                    h.Id,
                    h.Name,
                    h.Active,
                    h.CountryScopeId,
                    h.CompanyIco,
                    h.CompanyName,
                    h.ParentHospitalId,
                    PhoneNumber = h.ContactToObjects
                        .SelectMany(cto => cto.Contact.PhoneNumbers)
                        .Select(p => p.PhoneNumber)
                        .FirstOrDefault(),
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

            var actor = await _employeeScopeService.GetActorScopeAsync();
            if (actor.IsScopedAdmin)
            {
                var allowed = actor.IsCenterAdmin && !actor.IsCountryAdmin
                    ? actor.HospitalIds.Contains(hospital.Id)
                    : actor.AdministeredCountryScopeIds.Contains(hospital.CountryScopeId);

                if (!allowed)
                    return StatusCode(403, "K tomuto centru nemáte přístup.");
            }

            return Ok(hospital);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hospital");
            return StatusCode(500, new { Error = "Error getting hospital", Details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHospitalRequest request)
    {
        try
        {
            var trimmedName = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
                return BadRequest("Hospital name is required");

            var duplicateExists = await _context.Hospitals
                .AnyAsync(h => h.Name != null && h.Name.ToLower() == trimmedName.ToLower());

            if (duplicateExists)
                return Conflict("Hospital with the same name already exists");

            var scopeCheck = await CheckCountryScopeAllowedAsync(request.CountryScopeId ?? Hospital.CzechCountryScopeId);
            if (scopeCheck != null)
                return scopeCheck;

            var hospital = new Hospital
            {
                Name = trimmedName,
                CountryScopeId = request.CountryScopeId ?? Hospital.CzechCountryScopeId,
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

            return StatusCode(StatusCodes.Status201Created, new
            {
                hospital.Id,
                hospital.Name,
                hospital.CountryScopeId,
                hospital.Active,
                Address = hospital.AddressId == null ? null : new
                {
                    Street = request.Street ?? string.Empty,
                    City = request.City ?? string.Empty,
                    PostalCode = request.PostalCode ?? string.Empty,
                    Country = request.Country ?? string.Empty
                }
            });
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
                .AsNoTracking()
                .Include(h => h.Address)
                .FirstOrDefaultAsync(h => h.Id == id);
            if (hospital == null)
                return NotFound("Hospital not found");

            if (request.Name != null)
            {
                var trimmedName = request.Name.Trim();
                if (string.IsNullOrWhiteSpace(trimmedName))
                    return BadRequest("Hospital name is required");

                var duplicateExists = await _context.Hospitals
                    .AnyAsync(h => h.Id != id && h.Name != null && h.Name.ToLower() == trimmedName.ToLower());

                if (duplicateExists)
                    return Conflict("Hospital with the same name already exists");

                hospital.Name = trimmedName;
            }

            if (request.Active.HasValue)
                hospital.Active = request.Active;

            if (request.CountryScopeId.HasValue && request.CountryScopeId.Value > 0)
            {
                var scopeCheck = await CheckCountryScopeAllowedAsync(request.CountryScopeId.Value);
                if (scopeCheck != null)
                    return scopeCheck;
                hospital.CountryScopeId = request.CountryScopeId.Value;
            }

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
            return Ok(new
            {
                hospital.Id,
                hospital.Name,
                hospital.CountryScopeId,
                hospital.Active,
                Address = hospital.Address == null ? null : new
                {
                    hospital.Address.Street,
                    hospital.Address.City,
                    hospital.Address.PostalCode,
                    hospital.Address.Country
                }
            });
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
    public int? CountryScopeId { get; set; }
}

public class UpdateHospitalRequest
{
    public string? Name { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public int? CountryScopeId { get; set; }
    public bool? Active { get; set; }
}
