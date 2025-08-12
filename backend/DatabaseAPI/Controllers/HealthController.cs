using Microsoft.AspNetCore.Mvc;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "Database API",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0"
        });
    }
}
