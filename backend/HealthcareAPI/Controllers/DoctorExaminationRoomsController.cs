using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DoctorExaminationRoomsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DoctorExaminationRoomsController> _logger;

    public DoctorExaminationRoomsController(IHttpClientFactory httpClientFactory, ILogger<DoctorExaminationRoomsController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("doctor/{doctorId}")]
    public async Task<IActionResult> GetDoctorRooms(int doctorId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync($"/api/doctorexaminationrooms/doctor/{doctorId}");

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error retrieving examination rooms");

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting doctor examination rooms");
            return StatusCode(500, new { Error = "Error getting examination rooms", Details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> AssignRoom([FromBody] AssignExaminationRoomRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("/api/doctorexaminationrooms", httpContent);

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
            _logger.LogError(ex, "Error assigning examination room");
            return StatusCode(500, new { Error = "Error assigning examination room", Details = ex.Message });
        }
    }

    [HttpDelete("{assignmentId}")]
    public async Task<IActionResult> RemoveAssignment(int assignmentId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.DeleteAsync($"/api/doctorexaminationrooms/{assignmentId}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, error);
            }

            return Ok(new { Message = "Assignment removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing assignment");
            return StatusCode(500, new { Error = "Error removing assignment", Details = ex.Message });
        }
    }
}

public class AssignExaminationRoomRequest
{
    public int DoctorId { get; set; }
    public int ExaminationRoomId { get; set; }
}
