using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using HealthcareAPI.Models;
using HealthcareAPI.Services;

namespace HealthcareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DoctorsController : ControllerBase
    {
        private readonly IDoctorService _doctorService;

        public DoctorsController(IDoctorService doctorService)
        {
            _doctorService = doctorService;
        }

        [HttpGet("me")]
        public async Task<ActionResult<Doctor>> GetCurrentDoctor()
        {
            var doctorId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            var doctor = await _doctorService.GetCurrentDoctorAsync(doctorId);

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
            
            var stats = await _doctorService.GetDashboardStatsAsync(doctorId);

            if (stats == null)
            {
                return NotFound("Doktor nenalezen");
            }

            return Ok(stats);
        }
    }
}