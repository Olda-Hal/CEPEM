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
}
