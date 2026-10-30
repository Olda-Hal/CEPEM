using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.APIModels;
using DatabaseAPI.Services;

namespace DatabaseAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IEmployeeManagementService _employeeManagementService;
        private readonly IEmployeeScopeService _employeeScopeService;
        private readonly IPasswordResetService _passwordResetService;

        public AdminController(IEmployeeManagementService employeeManagementService, IEmployeeScopeService employeeScopeService, IPasswordResetService passwordResetService)
        {
            _employeeManagementService = employeeManagementService;
            _employeeScopeService = employeeScopeService;
            _passwordResetService = passwordResetService;
        }

        [HttpGet("employees")]
        public async Task<ActionResult<List<EmployeeListItem>>> GetAllEmployees()
        {
            var employees = await _employeeManagementService.GetAllEmployeesAsync();
            return Ok(employees);
        }

        /// <summary>
        /// Resolved administrative scope of the calling employee so the UI can
        /// mirror the server-side authorization rules (allowed roles, countries
        /// and centers; full access flag).
        /// </summary>
        [HttpGet("employees/scope")]
        public async Task<ActionResult<object>> GetActorScope()
        {
            var actor = await _employeeScopeService.GetActorScopeAsync();
            return Ok(new
            {
                actor.EmployeeId,
                actor.Roles,
                HasFullAccess = actor.HasFullAccess,
                actor.IsCountryAdmin,
                actor.IsCenterAdmin,
                actor.HospitalIds,
                actor.AdministeredCountryScopeIds,
                AssignableRoleNames = actor.HasFullAccess
                    ? (await _employeeManagementService.GetAllRolesAsync()).Select(r => r.Name).ToList()
                    : actor.AssignableRoleNames
            });
        }

        /// <summary>All employees as CSV (semicolon-separated, BOM for Excel).</summary>
        [HttpGet("employees/export")]
        public async Task<IActionResult> ExportEmployeesCsv()
        {
            var employees = await _employeeManagementService.GetAllEmployeesAsync();

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("UID;FullName;Email;Phone;Roles;Active;LastLogin");
            foreach (var employee in employees)
            {
                builder.AppendLine(string.Join(';',
                    CsvEscape(employee.UID),
                    CsvEscape(employee.FullName),
                    CsvEscape(employee.Email),
                    CsvEscape(employee.PhoneNumber),
                    CsvEscape(string.Join('|', employee.Roles)),
                    employee.Active ? "1" : "0",
                    employee.LastLoginAt?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));
            }

            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
                .Concat(System.Text.Encoding.UTF8.GetBytes(builder.ToString()))
                .ToArray();

            return File(bytes, "text/csv", $"employees-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
        }

        /// <summary>
        /// Sends a password-reset link to the employee's e-mail address
        /// (supervisor-triggered). The link is single-use and short-lived.
        /// </summary>
        [HttpPost("employees/{employeeId}/reset-password")]
        public async Task<ActionResult> ResetEmployeePassword(int employeeId)
        {
            var (ok, error) = await _passwordResetService.RequestResetForEmployeeAsync(employeeId);
            if (!ok)
            {
                return error == "Employee not found"
                    ? NotFound(error)
                    : BadRequest(error ?? "E-mail se nepodařilo odeslat");
            }
            return Ok(new { message = "Resetový odkaz byl odeslán na e-mail zaměstnance." });
        }

        private static string CsvEscape(string value)
        {
            return value.Contains(';') || value.Contains('"') || value.Contains('\n')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }

        [HttpGet("employees/{employeeId}")]
        public async Task<ActionResult<EmployeeListItem>> GetEmployee(int employeeId)
        {
            var employee = await _employeeManagementService.GetEmployeeByIdAsync(employeeId);
            if (employee == null)
            {
                return NotFound("Employee not found");
            }
            return Ok(employee);
        }

        [HttpPut("employees/{employeeId}")]
        public async Task<ActionResult<UpdateEmployeeResponse>> UpdateEmployee(int employeeId, UpdateEmployeeRequest request)
        {
            var result = await _employeeManagementService.UpdateEmployeeAsync(employeeId, request);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        [HttpPatch("employees/{employeeId}/deactivate")]
        public async Task<ActionResult> DeactivateEmployee(int employeeId)
        {
            var (found, forbidden) = await _employeeManagementService.DeactivateEmployeeAsync(employeeId);
            if (!found)
            {
                return NotFound("Employee not found");
            }
            if (forbidden != null)
            {
                return StatusCode(403, forbidden);
            }
            return Ok(new { message = "Employee deactivated successfully" });
        }

        [HttpGet("roles")]
        public async Task<ActionResult<List<RoleDto>>> GetAllRoles()
        {
            var roles = await _employeeManagementService.GetAllRolesAsync();
            return Ok(roles);
        }

        [HttpPost("roles")]
        public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleRequest request)
        {
            var role = await _employeeManagementService.CreateRoleAsync(request);
            if (role == null)
            {
                return BadRequest("Role already exists or is invalid");
            }

            return Ok(role);
        }

        /// <summary>
        /// Set or clear the role's post-login landing page (whitelisted SPA
        /// paths only — see RoleDefaultPaths).
        /// </summary>
        [HttpPut("roles/{id}/default-path")]
        public async Task<IActionResult> UpdateRoleDefaultPath(int id, [FromBody] UpdateRoleDefaultPathRequest request)
        {
            var (success, error) = await _employeeManagementService.UpdateRoleDefaultPathAsync(id, request.DefaultPath);
            if (!success)
            {
                return error == "Role not found" ? NotFound(error) : BadRequest(error);
            }

            return NoContent();
        }

        /// <summary>
        /// Delete a role. 409 when it is system-critical or still assigned to
        /// employees — the body carries the human-readable reason.
        /// </summary>
        [HttpDelete("roles/{id}")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var (success, error) = await _employeeManagementService.DeleteRoleAsync(id);
            if (!success)
            {
                return error == "Role not found" ? NotFound(error) : Conflict(error);
            }

            return NoContent();
        }
    }
}
