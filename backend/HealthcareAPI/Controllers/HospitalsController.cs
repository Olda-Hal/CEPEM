using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HospitalsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HospitalsController> _logger;

    public HospitalsController(IHttpClientFactory httpClientFactory, ILogger<HospitalsController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync("/api/hospitals");

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error retrieving hospitals");

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hospitals");
            return StatusCode(500, new { Error = "Error getting hospitals", Details = ex.Message });
        }
    }

    [HttpGet("{hospitalId}/examination-types")]
    public async Task<IActionResult> GetHospitalExaminationTypes(int hospitalId, [FromQuery] string language = "en")
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync($"/api/hospitals/{hospitalId}/examination-types?language={Uri.EscapeDataString(language)}");

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error retrieving hospital examination types");

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hospital examination types for hospital {HospitalId}", hospitalId);
            return StatusCode(500, new { Error = "Error getting hospital examination types", Details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHospitalRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("/api/hospitals", httpContent);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, error);
            }

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating hospital");
            return StatusCode(500, new { Error = "Error creating hospital", Details = ex.Message });
        }
    }

    [HttpPut("{hospitalId}")]
    public async Task<IActionResult> Update(int hospitalId, [FromBody] UpdateHospitalRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PutAsync($"/api/hospitals/{hospitalId}", httpContent);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, error);
            }

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating hospital");
            return StatusCode(500, new { Error = "Error updating hospital", Details = ex.Message });
        }
    }

    [HttpDelete("{hospitalId}")]
    public async Task<IActionResult> Delete(int hospitalId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.DeleteAsync($"/api/hospitals/{hospitalId}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, error);
            }

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
    public string Address { get; set; } = string.Empty;
}

public class UpdateHospitalRequest
{
    public string? Address { get; set; }
    public bool? Active { get; set; }
}
