using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly DatabaseContext _context;

    public HealthController(DatabaseContext context)
    {
        _context = context;
    }

    /// <summary>Readiness: the process is up and the database is reachable.</summary>
    [HttpGet]
    public async Task<ActionResult<HealthResponse>> GetHealth()
    {
        var databaseReady = await _context.Database.CanConnectAsync();

        return StatusCode(
            databaseReady ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable,
            new HealthResponse
            {
                Status = databaseReady ? "Healthy" : "Unhealthy",
                Service = "Database API",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0"
            });
    }

    /// <summary>Liveness: only proves the process serves HTTP.</summary>
    [HttpGet("live")]
    public ActionResult<HealthResponse> GetLiveness()
    {
        return Ok(new HealthResponse
        {
            Status = "Healthy",
            Service = "Database API",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0"
        });
    }
}
