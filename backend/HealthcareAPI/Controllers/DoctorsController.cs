using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using HealthcareAPI.Data;
using HealthcareAPI.Models;

namespace HealthcareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DoctorsController : ControllerBase
    {
        private readonly HealthcareDbContext _context;

        public DoctorsController(HealthcareDbContext context)
        {
            _context = context;
        }

        [HttpGet("me")]
        public async Task<ActionResult<Doctor>> GetCurrentDoctor()
        {
            var doctorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            var doctor = await _context.Doctors
                .Where(d => d.Id == doctorId && d.IsActive)
                .Select(d => new Doctor
                {
                    Id = d.Id,
                    FirstName = d.FirstName,
                    LastName = d.LastName,
                    Email = d.Email,
                    Specialization = d.Specialization,
                    LicenseNumber = d.LicenseNumber,
                    IsActive = d.IsActive,
                    CreatedAt = d.CreatedAt,
                    LastLoginAt = d.LastLoginAt
                })
                .FirstOrDefaultAsync();

            if (doctor == null)
            {
                return NotFound("Doktor nenalezen");
            }

            return Ok(doctor);
        }

        [HttpGet("dashboard-stats")]
        public async Task<ActionResult<object>> GetDashboardStats()
        {
            var doctorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            var doctor = await _context.Doctors
                .FirstOrDefaultAsync(d => d.Id == doctorId && d.IsActive);

            if (doctor == null)
            {
                return NotFound("Doktor nenalezen");
            }

            // Pro demo účely - v budoucnu lze rozšířit o skutečné statistiky
            var stats = new
            {
                TotalDoctors = await _context.Doctors.CountAsync(d => d.IsActive),
                MySpecialization = doctor.Specialization,
                LastLogin = doctor.LastLoginAt,
                SystemStatus = "Online"
            };

            return Ok(stats);
        }
    }
}