using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthController(IAuthService authService, IHttpClientFactory httpClientFactory)
        {
            _authService = authService;
            _httpClientFactory = httpClientFactory;
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

        /// <summary>
        /// Forgot-pass flow: forwards to DatabaseAPI which sends a reset link
        /// when the e-mail belongs to an employee (non-enumerating).
        /// </summary>
        [HttpPost("forgot-password")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/auth/forgot-password", content);
            var body = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, body);
        }

        /// <summary>Consume a reset token (link from the reset e-mail).</summary>
        [HttpPost("reset-password")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            {
                return BadRequest("Neplatné údaje pro obnovení hesla");
            }

            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/auth/reset-password", content);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return BadRequest(body);
            }
            return NoContent();
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
