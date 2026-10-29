using HealthcareAPI.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HealthcareAPI.Controllers;

/// <summary>
/// Only the confirm action lives here: it defaults the doctor to the
/// authenticated employee before forwarding to DatabaseAPI. All other
/// reservation-slot routes are handled by the YARP proxy (see ProxyConfig).
/// </summary>
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

    [RequirePermission("reservations.write")]
    [HttpPost("{slotId}/confirm")]
    public async Task<IActionResult> ConfirmSlot(int slotId, [FromBody] ConfirmReservationSlotRequest? request)
    {
        try
        {
            var doctorId = request?.DoctorId ?? 0;
            if (doctorId <= 0)
            {
                // Fall back to the authenticated employee
                var employeeIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (employeeIdClaim == null || !int.TryParse(employeeIdClaim, out var employeeId))
                {
                    return BadRequest(new { Error = "DoctorId must be provided or the caller must be an employee" });
                }
                doctorId = employeeId;
            }

            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(new ConfirmReservationSlotRequest { DoctorId = doctorId }),
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
            return StatusCode(500, new { Error = "Error confirming reservation slot" });
        }
    }
}

public class ConfirmReservationSlotRequest
{
    public int DoctorId { get; set; }
}
