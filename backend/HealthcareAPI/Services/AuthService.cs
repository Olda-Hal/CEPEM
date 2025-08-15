using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HealthcareAPI.Models;
using HealthcareAPI.Repositories;

namespace HealthcareAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly IDoctorRepository _doctorRepository;
        private readonly IConfiguration _configuration;

        public AuthService(IDoctorRepository doctorRepository, IConfiguration configuration)
        {
            _doctorRepository = doctorRepository;
            _configuration = configuration;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return null;
            }

            var doctor = await _doctorRepository.GetByEmailAsync(request.Email);
            if (doctor == null)
            {
                return null;
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, doctor.PasswordHash);
            if (!isPasswordValid)
            {
                return null;
            }

            await _doctorRepository.UpdateLastLoginAsync(doctor.Id, DateTime.UtcNow);

            var token = GenerateJwtToken(doctor);

            return new LoginResponse
            {
                Token = token,
                Email = doctor.Email,
                FullName = doctor.FullName,
                Specialization = doctor.Specialization
            };
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
