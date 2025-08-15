using Microsoft.AspNetCore.Mvc;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { Status = "Healthy", Service = "HealthcareAPI" });
    }
}
