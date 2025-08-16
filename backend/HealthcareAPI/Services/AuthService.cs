using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HealthcareAPI.Models;
using System.Diagnostics;

namespace HealthcareAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            Console.WriteLine("Processing login request for email: " + request.Email);
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                Console.WriteLine("Invalid login request: email or password is empty.");
                return null;
            }
            // Log the login attempt
            // Zavolej DatabaseAPI pro autentifikaci
            Console.WriteLine("Authenticating with DatabaseAPI for email: " + request.Email);
            var employeeInfo = await AuthenticateWithDatabaseAPI(request);
            if (employeeInfo == null)
            {
                return null;
            }

            var token = GenerateJwtToken(employeeInfo);

            return new LoginResponse
            {
                Token = token,
                Email = employeeInfo.Email,
                FullName = employeeInfo.FullName,
                Specialization = "Lékař" // Defaultní hodnota, můžeme rozšířit později
            };
        }

        private async Task<EmployeeAuthInfo?> AuthenticateWithDatabaseAPI(LoginRequest request)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                
                var json = JsonSerializer.Serialize(new
                {
                    Email = request.Email,
                    Password = request.Password
                });
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                Console.WriteLine("Sending authentication request to DatabaseAPI for email: " + request.Email);
                var response = await client.PostAsync("/api/auth/authenticate", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    return JsonSerializer.Deserialize<EmployeeAuthInfo>(responseContent, options);
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private string GenerateJwtToken(EmployeeAuthInfo employeeInfo)
        {
            var jwtSettings = _configuration.GetSection("JWT");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey ?? "test-secret-key-for-jwt-testing-minimum-256-bits-long"));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, employeeInfo.EmployeeId.ToString()),
                new Claim(ClaimTypes.Email, employeeInfo.Email),
                new Claim(ClaimTypes.Name, employeeInfo.FullName),
                new Claim("specialization", "Lékař") // Defaultní hodnota
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
