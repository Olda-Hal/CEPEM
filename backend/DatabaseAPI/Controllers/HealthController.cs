using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.APIModels;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthResponse> GetHealth()
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
