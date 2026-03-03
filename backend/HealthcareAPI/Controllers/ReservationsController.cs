using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReservationsController> _logger;

    public ReservationsController(IHttpClientFactory httpClientFactory, ILogger<ReservationsController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("my-reservations")]
    public async Task<IActionResult> GetMyReservations(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            var doctorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (doctorIdClaim == null || !int.TryParse(doctorIdClaim.Value, out int doctorId))
                return Unauthorized();

            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var queryParams = new List<string>();
            
            if (from.HasValue)
                queryParams.Add($"from={from.Value:O}");
            if (to.HasValue)
                queryParams.Add($"to={to.Value:O}");

            var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            var response = await client.GetAsync($"/api/reservations/doctor/{doctorId}{queryString}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"DatabaseAPI returned status {response.StatusCode}");
                return StatusCode((int)response.StatusCode, "Error retrieving reservations");
            }

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reservations");
            return StatusCode(500, new { Error = "Error getting reservations", Details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationRequest request)
    {
        try
        {
            var doctorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (doctorIdClaim == null || !int.TryParse(doctorIdClaim.Value, out int doctorId))
                return Unauthorized();

            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var selectedDoctorId = request.DoctorId ?? doctorId;

            var createRequest = new
            {
                doctorId = selectedDoctorId,
                patientId = request.PatientId,
                examinationRoomId = request.ExaminationRoomId,
                examinationTypeId = request.ExaminationTypeId,
                startDateTime = request.StartDateTime,
                endDateTime = request.EndDateTime,
                notes = request.Notes
            };

            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(createRequest),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("/api/reservations", httpContent);

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
            _logger.LogError(ex, "Error creating reservation");
            return StatusCode(500, new { Error = "Error creating reservation", Details = ex.Message });
        }
    }

    [HttpPut("{reservationId}")]
    public async Task<IActionResult> UpdateReservation(int reservationId, [FromBody] UpdateReservationRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PutAsync($"/api/reservations/{reservationId}", httpContent);

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
            _logger.LogError(ex, "Error updating reservation");
            return StatusCode(500, new { Error = "Error updating reservation", Details = ex.Message });
        }
    }

    [HttpDelete("{reservationId}")]
    public async Task<IActionResult> DeleteReservation(int reservationId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.DeleteAsync($"/api/reservations/{reservationId}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, error);
            }

            return Ok(new { Message = "Reservation deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reservation");
            return StatusCode(500, new { Error = "Error deleting reservation", Details = ex.Message });
        }
    }

    [HttpGet("room/{roomId}")]
    public async Task<IActionResult> GetRoomReservations(
        int roomId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var queryParams = new List<string>();
            
            if (from.HasValue)
                queryParams.Add($"from={from.Value:O}");
            if (to.HasValue)
                queryParams.Add($"to={to.Value:O}");

            var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            var response = await client.GetAsync($"/api/reservations/room/{roomId}{queryString}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"DatabaseAPI returned status {response.StatusCode}");
                return StatusCode((int)response.StatusCode, "Error retrieving reservations");
            }

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room reservations");
            return StatusCode(500, new { Error = "Error getting reservations", Details = ex.Message });
        }
    }
}

public class CreateReservationRequest
{
    public int? DoctorId { get; set; }
    public int PatientId { get; set; }
    public int ExaminationRoomId { get; set; }
    public int ExaminationTypeId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? Notes { get; set; }
}

public class UpdateReservationRequest
{
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public string? Notes { get; set; }
    public string? Status { get; set; }
}
