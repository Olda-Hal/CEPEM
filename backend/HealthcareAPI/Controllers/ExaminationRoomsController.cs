using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExaminationRoomsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExaminationRoomsController> _logger;

    public ExaminationRoomsController(IHttpClientFactory httpClientFactory, ILogger<ExaminationRoomsController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("hospital/{hospitalId}")]
    public async Task<IActionResult> GetHospitalRooms(int hospitalId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync($"/api/examinationrooms/{hospitalId}");

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error retrieving examination rooms");

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting examination rooms");
            return StatusCode(500, new { Error = "Error getting examination rooms", Details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] CreateExaminationRoomRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("/api/examinationrooms", httpContent);

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
            _logger.LogError(ex, "Error creating examination room");
            return StatusCode(500, new { Error = "Error creating examination room", Details = ex.Message });
        }
    }

    [HttpPut("{roomId}")]
    public async Task<IActionResult> UpdateRoom(int roomId, [FromBody] UpdateExaminationRoomRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PutAsync($"/api/examinationrooms/{roomId}", httpContent);

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
            _logger.LogError(ex, "Error updating examination room");
            return StatusCode(500, new { Error = "Error updating examination room", Details = ex.Message });
        }
    }

    [HttpDelete("{roomId}")]
    public async Task<IActionResult> DeleteRoom(int roomId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.DeleteAsync($"/api/examinationrooms/{roomId}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, error);
            }

            return Ok(new { Message = "Room deactivated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting examination room");
            return StatusCode(500, new { Error = "Error deleting examination room", Details = ex.Message });
        }
    }
}

public class CreateExaminationRoomRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int HospitalId { get; set; }
}

public class UpdateExaminationRoomRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}
