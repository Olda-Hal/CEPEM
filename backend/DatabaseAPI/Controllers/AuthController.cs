using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.Models;
using DatabaseAPI.Services;
using System.Diagnostics;

namespace DatabaseAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IEmployeeAuthService _authService;

        public AuthController(IEmployeeAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("authenticate")]
        public async Task<ActionResult<EmployeeAuthInfo>> Authenticate([FromBody] LoginRequest request)
        {
            Console.WriteLine("Přihlášení uživatele Z DATABASEAPI: " + request.Email);
            var employeeInfo = await _authService.AuthenticateAsync(request.Email, request.Password);

            if (employeeInfo == null)
            {
                return Unauthorized("Neplatné přihlašovací údaje");
            }

            // Aktualizuj čas posledního přihlášení
            await _authService.UpdateLastLoginAsync(employeeInfo.EmployeeId);

            return Ok(employeeInfo);
        }
    }
}
