using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public EventsController(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("DatabaseAPI");
    }

    [HttpGet("options")]
    public async Task<IActionResult> GetEventOptions()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/events/options");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error forwarding request to DatabaseAPI: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateEvent([FromBody] JsonElement eventData)
    {
        try
        {
            var json = JsonSerializer.Serialize(eventData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/events", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error forwarding request to DatabaseAPI: {ex.Message}");
        }
    }

    [HttpPost("examination-types")]
    public async Task<IActionResult> CreateExaminationType([FromBody] JsonElement requestData)
    {
        try
        {
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/events/examination-types", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error forwarding request to DatabaseAPI: {ex.Message}");
        }
    }

    [HttpPost("symptoms")]
    public async Task<IActionResult> CreateSymptom([FromBody] JsonElement requestData)
    {
        try
        {
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/events/symptoms", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error forwarding request to DatabaseAPI: {ex.Message}");
        }
    }

    [HttpPost("injury-types")]
    public async Task<IActionResult> CreateInjuryType([FromBody] JsonElement requestData)
    {
        try
        {
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/events/injury-types", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error forwarding request to DatabaseAPI: {ex.Message}");
        }
    }

    [HttpPost("vaccine-types")]
    public async Task<IActionResult> CreateVaccineType([FromBody] JsonElement requestData)
    {
        try
        {
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/events/vaccine-types", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error forwarding request to DatabaseAPI: {ex.Message}");
        }
    }

    [HttpPost("group")]
    public async Task<IActionResult> CreateEventGroup([FromBody] JsonElement eventGroupData)
    {
        try
        {
            var json = JsonSerializer.Serialize(eventGroupData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/events/group", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error forwarding request to DatabaseAPI: {ex.Message}");
        }
    }

    [HttpPost("drugs")]
    public async Task<IActionResult> CreateDrug([FromBody] JsonElement requestData)
    {
        try
        {
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/events/drugs", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error forwarding request to DatabaseAPI: {ex.Message}");
        }
    }

    [HttpPost("drug-categories")]
    public async Task<IActionResult> CreateDrugCategory([FromBody] JsonElement requestData)
    {
        try
        {
            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/events/drug-categories", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return Content(responseContent, "application/json");
            }
            
            return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error forwarding request to DatabaseAPI: {ex.Message}");
        }
    }
}
