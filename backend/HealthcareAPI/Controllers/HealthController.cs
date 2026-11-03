using Microsoft.AspNetCore.Mvc;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HealthController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>Readiness: the gateway can reach DatabaseAPI and its database.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync("/api/health");
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new { Status = "Unhealthy", Service = "HealthcareAPI", Dependency = "DatabaseAPI" });
            }

            return Ok(new { Status = "Healthy", Service = "HealthcareAPI" });
        }
        catch
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { Status = "Unhealthy", Service = "HealthcareAPI", Dependency = "DatabaseAPI" });
        }
    }

    /// <summary>Liveness: only proves the process serves HTTP.</summary>
    [HttpGet("live")]
    public IActionResult GetLiveness() => Ok(new { Status = "Healthy", Service = "HealthcareAPI" });
}
