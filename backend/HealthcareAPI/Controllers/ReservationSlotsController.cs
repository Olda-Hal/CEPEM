using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("api/reservations/slots")]
[Authorize]
public class ReservationSlotsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReservationSlotsController> _logger;

    public ReservationSlotsController(IHttpClientFactory httpClientFactory, ILogger<ReservationSlotsController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("hospital/{hospitalId}")]
    public async Task<IActionResult> GetByHospital(int hospitalId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? status)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var queryParams = new List<string>();

            if (from.HasValue)
                queryParams.Add($"from={from.Value:O}");
            if (to.HasValue)
                queryParams.Add($"to={to.Value:O}");
            if (!string.IsNullOrWhiteSpace(status))
                queryParams.Add($"status={Uri.EscapeDataString(status)}");

            var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            var response = await client.GetAsync($"/api/reservations/slots/hospital/{hospitalId}{queryString}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("DatabaseAPI returned status {StatusCode}", response.StatusCode);
                return StatusCode((int)response.StatusCode, "Error retrieving reservation slots");
            }

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting reservation slots");
            return StatusCode(500, new { Error = "Error getting reservation slots", Details = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateSlots([FromBody] CreateReservationSlotsRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("/api/reservations/slots", httpContent);

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
            _logger.LogError(ex, "Error creating reservation slots");
            return StatusCode(500, new { Error = "Error creating reservation slots", Details = ex.Message });
        }
    }

    [HttpPost("hospital/{hospitalId}/copy-day")]
    public async Task<IActionResult> CopyDaySlots(int hospitalId, [FromBody] CopyReservationSlotsDayRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync($"/api/reservations/slots/hospital/{hospitalId}/copy-day", httpContent);

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
            _logger.LogError(ex, "Error copying reservation slots");
            return StatusCode(500, new { Error = "Error copying reservation slots", Details = ex.Message });
        }
    }

    [HttpPut("{slotId}")]
    public async Task<IActionResult> UpdateSlot(int slotId, [FromBody] UpdateReservationSlotRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PutAsync($"/api/reservations/slots/{slotId}", httpContent);

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
            _logger.LogError(ex, "Error updating reservation slot");
            return StatusCode(500, new { Error = "Error updating reservation slot", Details = ex.Message });
        }
    }

    [HttpPost("{slotId}/release")]
    public async Task<IActionResult> ReleaseSlot(int slotId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.PostAsync($"/api/reservations/slots/{slotId}/release", null);

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
            _logger.LogError(ex, "Error releasing reservation slot");
            return StatusCode(500, new { Error = "Error releasing reservation slot", Details = ex.Message });
        }
    }

    [HttpDelete("{slotId}")]
    public async Task<IActionResult> DeleteSlot(int slotId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.DeleteAsync($"/api/reservations/slots/{slotId}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, error);
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reservation slot");
            return StatusCode(500, new { Error = "Error deleting reservation slot", Details = ex.Message });
        }
    }

    [HttpPost("{slotId}/block")]
    public async Task<IActionResult> BlockSlot(int slotId, [FromBody] BlockReservationSlotRequest request)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync($"/api/reservations/slots/{slotId}/block", httpContent);

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
            _logger.LogError(ex, "Error blocking reservation slot");
            return StatusCode(500, new { Error = "Error blocking reservation slot", Details = ex.Message });
        }
    }

    [HttpPost("{slotId}/confirm")]
    public async Task<IActionResult> ConfirmSlot(int slotId, [FromBody] ConfirmReservationSlotRequest? request)
    {
        try
        {
            var doctorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if ((request == null || request.DoctorId <= 0) && (doctorIdClaim == null || !int.TryParse(doctorIdClaim.Value, out _)))
                return Unauthorized();

            var doctorId = request?.DoctorId > 0 && request != null
                ? request.DoctorId
                : int.Parse(doctorIdClaim!.Value);

            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var payload = new ConfirmReservationSlotRequest { DoctorId = doctorId };
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync($"/api/reservations/slots/{slotId}/confirm", httpContent);

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
            _logger.LogError(ex, "Error confirming reservation slot");
            return StatusCode(500, new { Error = "Error confirming reservation slot", Details = ex.Message });
        }
    }

    [HttpPost("{slotId}/reject")]
    public async Task<IActionResult> RejectSlot(int slotId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.PostAsync($"/api/reservations/slots/{slotId}/reject", null);

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
            _logger.LogError(ex, "Error rejecting reservation slot");
            return StatusCode(500, new { Error = "Error rejecting reservation slot", Details = ex.Message });
        }
    }
}

public class CreateReservationSlotsRequest
{
    public int HospitalId { get; set; }
    public List<CreateReservationSlotItemRequest> Slots { get; set; } = new();
}

public class CreateReservationSlotItemRequest
{
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? PublicNote { get; set; }
    public string? InternalNote { get; set; }
    public string? Status { get; set; }
}

public class UpdateReservationSlotRequest
{
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public string? PublicNote { get; set; }
    public string? InternalNote { get; set; }
    public string? Status { get; set; }
}

public class BlockReservationSlotRequest
{
    public int? PersonId { get; set; }
    public BlockReservationSlotPersonRequest? NewPerson { get; set; }
    public int ExaminationTypeId { get; set; }
}

public class BlockReservationSlotPersonRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
}

public class ConfirmReservationSlotRequest
{
    public int DoctorId { get; set; }
}

public class CopyReservationSlotsDayRequest
{
    public DateTime SourceDate { get; set; }
    public DateTime TargetDate { get; set; }
    public bool PreserveStatus { get; set; }
}