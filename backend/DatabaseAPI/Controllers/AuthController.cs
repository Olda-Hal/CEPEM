using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.APIModels;
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
            var result = await _authService.AuthenticateDetailedAsync(request.Email, request.Password);

            switch (result.Result)
            {
                case AuthenticationResult.Success:
                    // Aktualizuj čas posledního přihlášení
                    await _authService.UpdateLastLoginAsync(result.EmployeeInfo!.EmployeeId);
                    return Ok(result.EmployeeInfo);

                case AuthenticationResult.AccountDeactivated:
                    return Unauthorized("Váš účet byl deaktivován. Kontaktujte administrátora.");

                case AuthenticationResult.UserNotFound:
                case AuthenticationResult.InvalidCredentials:
                default:
                    return Unauthorized("Neplatné přihlašovací údaje");
            }
        }

        [HttpPost("change-password/{employeeId}")]
        public async Task<ActionResult> ChangePassword(int employeeId, [FromBody] ChangePasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.NewPassword) || 
                string.IsNullOrEmpty(request.CurrentPassword) ||
                request.NewPassword != request.ConfirmPassword)
            {
                return BadRequest("Neplatné údaje pro změnu hesla");
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest("Nové heslo musí mít alespoň 6 znaků");
            }

            var result = await _authService.ChangePasswordDetailedAsync(employeeId, request.CurrentPassword, request.NewPassword);

            switch (result.Result)
            {
                case PasswordChangeResult.Success:
                    return Ok(new { message = "Heslo bylo úspěšně změněno" });
                case PasswordChangeResult.InvalidCurrentPassword:
                    return BadRequest("Současné heslo je nesprávné");
                case PasswordChangeResult.SamePassword:
                    return BadRequest("Nové heslo nemůže být stejné jako současné heslo");
                default:
                    return BadRequest("Došlo k chybě při změně hesla");
            }
        }

        [HttpPost("create-employee")]
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

            if (request.Password.Length < 6)
            {
                return BadRequest("Heslo musí mít alespoň 6 znaků");
            }

            var result = await _authService.CreateEmployeeAsync(request);

            if (result == null)
            {
                return BadRequest("Nepodařilo se vytvořit uživatele. Email nebo UID již existuje.");
            }

            return Ok(result);
        }

        [HttpGet("next-uid")]
        public async Task<ActionResult<string>> GetNextAvailableUid()
        {
            var nextUid = await _authService.GetNextAvailableUidAsync();
            return Ok(nextUid);
        }
    }
}
