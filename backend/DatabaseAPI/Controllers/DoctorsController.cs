using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.Models;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoctorsController : ControllerBase
{
    private readonly DatabaseContext _context;

    public DoctorsController(DatabaseContext context)
    {
        _context = context;
    }

    // GET: api/doctors
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Doctor>>> GetDoctors()
    {
        return await _context.Doctors
            .Where(d => d.IsActive)
            .ToListAsync();
    }

    // GET: api/doctors/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Doctor>> GetDoctor(int id)
    {
        var doctor = await _context.Doctors.FindAsync(id);

        if (doctor == null || !doctor.IsActive)
        {
            return NotFound();
        }

        return doctor;
    }

    // POST: api/doctors
    [HttpPost]
    public async Task<ActionResult<Doctor>> PostDoctor(Doctor doctor)
    {
        doctor.CreatedAt = DateTime.UtcNow;
        _context.Doctors.Add(doctor);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetDoctor), new { id = doctor.Id }, doctor);
    }

    // PUT: api/doctors/5
    [HttpPut("{id}")]
    public async Task<IActionResult> PutDoctor(int id, Doctor doctor)
    {
        if (id != doctor.Id)
        {
            return BadRequest();
        }

        doctor.UpdatedAt = DateTime.UtcNow;
        _context.Entry(doctor).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!DoctorExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return NoContent();
    }

    // DELETE: api/doctors/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDoctor(int id)
    {
        var doctor = await _context.Doctors.FindAsync(id);
        if (doctor == null)
        {
            return NotFound();
        }

        // Soft delete
        doctor.IsActive = false;
        doctor.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool DoctorExists(int id)
    {
        return _context.Doctors.Any(e => e.Id == id);
    }
}
