using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/hospitals/{hospitalId}/examination-types")]
public class HospitalExaminationTypesController : ControllerBase
{
    private readonly DatabaseContext _context;

    public HospitalExaminationTypesController(DatabaseContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<object>>> GetAllowed(int hospitalId, [FromQuery] string? language = "cs")
    {
        var exists = await _context.Hospitals.AnyAsync(h => h.Id == hospitalId);
        if (!exists)
            return NotFound("Hospital not found");

        var allowed = await _context.HospitalExaminationTypes
            .Where(het => het.HospitalId == hospitalId)
            .Include(het => het.ExaminationType)
                .ThenInclude(et => et.NameTranslation)
            .Select(het => new
            {
                het.ExaminationType.Id,
                Name = language == "cs"
                    ? het.ExaminationType.NameTranslation!.CS ?? het.ExaminationType.NameTranslation.EN
                    : language == "nl"
                        ? het.ExaminationType.NameTranslation!.NL ?? het.ExaminationType.NameTranslation.EN
                        : het.ExaminationType.NameTranslation!.EN
            })
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Ok(allowed);
    }

    [HttpPost("{examinationTypeId}")]
    public async Task<IActionResult> Allow(int hospitalId, int examinationTypeId)
    {
        var hospitalExists = await _context.Hospitals.AnyAsync(h => h.Id == hospitalId);
        if (!hospitalExists)
            return NotFound("Hospital not found");

        var typeExists = await _context.ExaminationTypes.AnyAsync(et => et.Id == examinationTypeId);
        if (!typeExists)
            return NotFound("Examination type not found");

        var alreadyExists = await _context.HospitalExaminationTypes
            .AnyAsync(het => het.HospitalId == hospitalId && het.ExaminationTypeId == examinationTypeId);
        if (alreadyExists)
            return Conflict("Examination type already allowed for this hospital");

        _context.HospitalExaminationTypes.Add(new HospitalExaminationType
        {
            HospitalId = hospitalId,
            ExaminationTypeId = examinationTypeId
        });
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("{examinationTypeId}")]
    public async Task<IActionResult> Disallow(int hospitalId, int examinationTypeId)
    {
        var entry = await _context.HospitalExaminationTypes
            .FirstOrDefaultAsync(het => het.HospitalId == hospitalId && het.ExaminationTypeId == examinationTypeId);

        if (entry == null)
            return NotFound("Examination type not allowed for this hospital");

        _context.HospitalExaminationTypes.Remove(entry);
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpPut]
    public async Task<IActionResult> SetAll(int hospitalId, [FromBody] int[] examinationTypeIds)
    {
        var hospitalExists = await _context.Hospitals.AnyAsync(h => h.Id == hospitalId);
        if (!hospitalExists)
            return NotFound("Hospital not found");

        var existing = await _context.HospitalExaminationTypes
            .Where(het => het.HospitalId == hospitalId)
            .ToListAsync();

        _context.HospitalExaminationTypes.RemoveRange(existing);

        foreach (var id in examinationTypeIds.Distinct())
        {
            var typeExists = await _context.ExaminationTypes.AnyAsync(et => et.Id == id);
            if (typeExists)
            {
                _context.HospitalExaminationTypes.Add(new HospitalExaminationType
                {
                    HospitalId = hospitalId,
                    ExaminationTypeId = id
                });
            }
        }

        await _context.SaveChangesAsync();
        return Ok();
    }
}
