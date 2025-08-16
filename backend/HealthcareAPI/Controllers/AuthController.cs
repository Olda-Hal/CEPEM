using Microsoft.AspNetCore.Mvc;
using HealthcareAPI.Models;
using HealthcareAPI.Services;

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
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
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
    }
}
