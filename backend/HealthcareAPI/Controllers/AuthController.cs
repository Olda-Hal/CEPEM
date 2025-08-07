using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using HealthcareAPI.Data;
using HealthcareAPI.Models;

namespace HealthcareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly HealthcareDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(HealthcareDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Email a heslo jsou povinné");
            }

            var doctor = await _context.Doctors
                .FirstOrDefaultAsync(d => d.Email == request.Email && d.IsActive);

            if (doctor == null)
            {
                return Unauthorized("Neplatné přihlašovací údaje");
            }

            // Verify password
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, doctor.PasswordHash);
            if (!isPasswordValid)
            {
                return Unauthorized("Neplatné přihlašovací údaje");
            }

            // Update last login
            doctor.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Generate JWT token
            var token = GenerateJwtToken(doctor);

            return Ok(new LoginResponse
            {
                Token = token,
                Email = doctor.Email,
                FullName = doctor.FullName,
                Specialization = doctor.Specialization
            });
        }

        private string GenerateJwtToken(Doctor doctor)
        {
            var jwtSettings = _configuration.GetSection("JWT");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, doctor.Id.ToString()),
                new Claim(ClaimTypes.Email, doctor.Email),
                new Claim(ClaimTypes.Name, doctor.FullName),
                new Claim("specialization", doctor.Specialization)
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
