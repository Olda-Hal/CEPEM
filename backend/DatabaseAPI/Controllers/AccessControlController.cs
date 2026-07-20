using DatabaseAPI.APIModels;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/access-control")]
public class AccessControlController : ControllerBase
{
    private readonly IAccessControlService _service;

    public AccessControlController(IAccessControlService service)
    {
        _service = service;
    }

    [HttpPost("evaluate")]
    public async Task<ActionResult<AccessEvaluationResponse>> Evaluate([FromBody] AccessEvaluationRequest request)
    {
        if (request.EmployeeId <= 0 || string.IsNullOrWhiteSpace(request.PermissionKey))
            return BadRequest("Invalid evaluation request");

        var result = await _service.EvaluateAsync(request);
        return Ok(result);
    }

    [HttpGet("employees/{employeeId}/rules")]
    public async Task<ActionResult<EmployeePermissionRulesResponse>> GetEmployeeRules(int employeeId)
    {
        var result = await _service.GetEmployeeRulesAsync(employeeId);
        if (result == null)
            return NotFound("Employee not found");

        return Ok(result);
    }

    [HttpPut("employees/{employeeId}/rules")]
    public async Task<IActionResult> UpdateEmployeeRules(int employeeId, [FromBody] UpdateEmployeePermissionRulesRequest request)
    {
        var success = await _service.UpdateEmployeeRulesAsync(employeeId, request);
        if (!success)
            return NotFound("Employee not found");

        return Ok(new { Message = "Access rules updated" });
    }

    [HttpGet("roles/{roleId}/rules")]
    public async Task<ActionResult<RolePermissionRulesResponse>> GetRoleRules(int roleId)
    {
        var result = await _service.GetRoleRulesAsync(roleId);
        if (result == null)
            return NotFound("Role not found");

        return Ok(result);
    }

    [HttpPut("roles/{roleId}/rules")]
    public async Task<IActionResult> UpdateRoleRules(int roleId, [FromBody] UpdateRolePermissionRulesRequest request)
    {
        var success = await _service.UpdateRoleRulesAsync(roleId, request);
        if (!success)
            return NotFound("Role not found");

        return Ok(new { Message = "Role access rules updated" });
    }

    [HttpGet("context/reservation-slots/{slotId}/hospital")]
    public async Task<ActionResult<AccessResourceContextDto>> GetHospitalContextForReservationSlot(int slotId)
    {
        var hospitalId = await _service.GetHospitalIdForReservationSlotAsync(slotId);
        if (!hospitalId.HasValue)
            return NotFound("Reservation slot not found");

        return Ok(new AccessResourceContextDto { ResourceType = "Hospital", ResourceId = hospitalId.Value });
    }

    [HttpGet("context/examination-rooms/{roomId}/hospital")]
    public async Task<ActionResult<AccessResourceContextDto>> GetHospitalContextForExaminationRoom(int roomId)
    {
        var hospitalId = await _service.GetHospitalIdForExaminationRoomAsync(roomId);
        if (!hospitalId.HasValue)
            return NotFound("Examination room not found");

        return Ok(new AccessResourceContextDto { ResourceType = "Hospital", ResourceId = hospitalId.Value });
    }

    [HttpGet("context/doctor-room-assignments/{assignmentId}/hospital")]
    public async Task<ActionResult<AccessResourceContextDto>> GetHospitalContextForDoctorRoomAssignment(int assignmentId)
    {
        var hospitalId = await _service.GetHospitalIdForDoctorRoomAssignmentAsync(assignmentId);
        if (!hospitalId.HasValue)
            return NotFound("Doctor room assignment not found");

        return Ok(new AccessResourceContextDto { ResourceType = "Hospital", ResourceId = hospitalId.Value });
    }

    [HttpGet("context/hospitals/{hospitalId}/country")]
    public async Task<ActionResult<AccessResourceContextDto>> GetCountryContextForHospital(int hospitalId)
    {
        var countryScopeId = await _service.GetCountryScopeIdForHospitalAsync(hospitalId);
        if (!countryScopeId.HasValue)
            return NotFound("Hospital not found");

        return Ok(new AccessResourceContextDto { ResourceType = "Country", ResourceId = countryScopeId.Value });
    }

    [HttpGet("context/reservation-slots/{slotId}/country")]
    public async Task<ActionResult<AccessResourceContextDto>> GetCountryContextForReservationSlot(int slotId)
    {
        var countryScopeId = await _service.GetCountryScopeIdForReservationSlotAsync(slotId);
        if (!countryScopeId.HasValue)
            return NotFound("Reservation slot not found");

        return Ok(new AccessResourceContextDto { ResourceType = "Country", ResourceId = countryScopeId.Value });
    }

    [HttpGet("context/examination-rooms/{roomId}/country")]
    public async Task<ActionResult<AccessResourceContextDto>> GetCountryContextForExaminationRoom(int roomId)
    {
        var countryScopeId = await _service.GetCountryScopeIdForExaminationRoomAsync(roomId);
        if (!countryScopeId.HasValue)
            return NotFound("Examination room not found");

        return Ok(new AccessResourceContextDto { ResourceType = "Country", ResourceId = countryScopeId.Value });
    }

    [HttpGet("context/doctor-room-assignments/{assignmentId}/country")]
    public async Task<ActionResult<AccessResourceContextDto>> GetCountryContextForDoctorRoomAssignment(int assignmentId)
    {
        var countryScopeId = await _service.GetCountryScopeIdForDoctorRoomAssignmentAsync(assignmentId);
        if (!countryScopeId.HasValue)
            return NotFound("Doctor room assignment not found");

        return Ok(new AccessResourceContextDto { ResourceType = "Country", ResourceId = countryScopeId.Value });
    }
}
