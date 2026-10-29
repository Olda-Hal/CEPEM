using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using HealthcareAPI.Models;
using HealthcareAPI.Services;
using HealthcareAPI.Middleware;

namespace HealthcareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                Console.WriteLine("Login attempt received.");
                var loginResponse = await _authService.LoginAsync(request);
                Console.WriteLine("Login response generated.");
                if (loginResponse == null)
                {
                    return Unauthorized("Neplatné přihlašovací údaje");
                }

                return Ok(loginResponse);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Return specific error message for deactivated accounts or invalid credentials
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var employeeIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (employeeIdClaim == null || !int.TryParse(employeeIdClaim.Value, out int employeeId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrEmpty(request.NewPassword) || 
                string.IsNullOrEmpty(request.CurrentPassword) ||
                request.NewPassword != request.ConfirmPassword)
            {
                return BadRequest("Neplatné údaje pro změnu hesla");
            }

            if (request.NewPassword.Length < 8)
            {
                return BadRequest("Nové heslo musí mít alespoň 8 znaků");
            }

            if (!request.NewPassword.Any(char.IsDigit) || !request.NewPassword.Any(char.IsLetter))
            {
                return BadRequest("Nové heslo musí obsahovat alespoň jedno písmeno a jednu číslici");
            }

            var success = await _authService.ChangePasswordAsync(employeeId, request);

            if (!success)
            {
                return BadRequest("Současné heslo je nesprávné nebo došlo k chybě");
            }

            return Ok(new { message = "Heslo bylo úspěšně změněno" });
        }

        [RequirePermission("employees.create")]
        [EnableRateLimiting("api-write")]
        [HttpPost("create-employee")]
        [Authorize]
        public async Task<ActionResult<CreateEmployeeResponse>> CreateEmployee([FromBody] CreateEmployeeRequest request)
        {
            if (string.IsNullOrEmpty(request.FirstName) || 
                string.IsNullOrEmpty(request.LastName) ||
                string.IsNullOrEmpty(request.Email) ||
                string.IsNullOrEmpty(request.PhoneNumber) ||
                string.IsNullOrEmpty(request.UID) ||
                string.IsNullOrEmpty(request.Gender) ||
                string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Všechna povinná pole musí být vyplněna");
            }

            if (request.Password.Length < 8)
            {
                return BadRequest("Heslo musí mít alespoň 8 znaků");
            }

            if (!request.Password.Any(char.IsDigit) || !request.Password.Any(char.IsLetter))
            {
                return BadRequest("Heslo musí obsahovat alespoň jedno písmeno a jednu číslici");
            }

            var (result, errorBody) = await _authService.CreateEmployeeAsync(request);

            if (result == null)
            {
                if (errorBody != null)
                {
                    return BadRequest(errorBody);
                }
                return BadRequest("Nepodařilo se vytvořit uživatele. Email nebo UID již existuje.");
            }

            return Ok(result);
        }

        [RequirePermission("uid.read")]
        [HttpGet("next-uid")]
        [Authorize]
        public async Task<ActionResult<string>> GetNextAvailableUid()
        {
            var nextUid = await _authService.GetNextAvailableUidAsync();
            return Ok(nextUid);
        }
    }
}
