using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExaminationTypesController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExaminationTypesController> _logger;

    public ExaminationTypesController(IHttpClientFactory httpClientFactory, ILogger<ExaminationTypesController> logger)
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
            var response = await client.GetAsync("/api/examinationtypes");

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error retrieving examination types");

            var content = await response.Content.ReadAsStringAsync();
            return Ok(System.Text.Json.JsonSerializer.Deserialize<object>(content));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting examination types");
            return StatusCode(500, new { Error = "Error getting examination types" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync($"/api/examinationtypes/{id}");

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error retrieving examination type");

            var content = await response.Content.ReadAsStringAsync();
            return Ok(System.Text.Json.JsonSerializer.Deserialize<object>(content));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting examination type");
            return StatusCode(500, new { Error = "Error getting examination type" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/examinationtypes", content);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error creating examination type");

            var responseContent = await response.Content.ReadAsStringAsync();
            return Ok(System.Text.Json.JsonSerializer.Deserialize<object>(responseContent));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating examination type");
            return StatusCode(500, new { Error = "Error creating examination type" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] object request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var json = System.Text.Json.JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"/api/examinationtypes/{id}", content);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error updating examination type");

            var responseContent = await response.Content.ReadAsStringAsync();
            return Ok(System.Text.Json.JsonSerializer.Deserialize<object>(responseContent));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating examination type");
            return StatusCode(500, new { Error = "Error updating examination type" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.DeleteAsync($"/api/examinationtypes/{id}");

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error deleting examination type");

            var content = await response.Content.ReadAsStringAsync();
            return Ok(System.Text.Json.JsonSerializer.Deserialize<object>(content));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting examination type");
            return StatusCode(500, new { Error = "Error deleting examination type" });
        }
    }
}
