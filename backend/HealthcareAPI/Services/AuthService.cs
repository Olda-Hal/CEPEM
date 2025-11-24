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
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return null;
            }
            
            try
            {
                // Zavolej DatabaseAPI pro autentifikaci
                var employeeInfo = await AuthenticateWithDatabaseAPI(request);
                if (employeeInfo == null)
                {
                    return null;
                }

                var token = GenerateJwtToken(employeeInfo);

                // Check if password has expired
                var requiresPasswordChange = employeeInfo.PasswordExpiration.HasValue && 
                                           employeeInfo.PasswordExpiration.Value.Date <= DateTime.UtcNow.Date;

                return new LoginResponse
                {
                    Token = token,
                    Email = employeeInfo.Email,
                    FullName = employeeInfo.FullName,
                    PasswordExpiration = employeeInfo.PasswordExpiration,
                    RequiresPasswordChange = requiresPasswordChange
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                // Return a special response for deactivated accounts
                throw new UnauthorizedAccessException(ex.Message);
            }
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
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Parse the error message from the response
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    // You could throw a specific exception here or handle different error cases
                    throw new UnauthorizedAccessException(errorMessage.Trim('"'));
                }

                return null;
            }
            catch (UnauthorizedAccessException)
            {
                // Re-throw unauthorized exceptions to preserve the error message
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private string GenerateJwtToken(EmployeeAuthInfo employeeInfo)
        {
            var jwtSettings = _configuration.GetSection("JWT");
            var secretKey = jwtSettings["SecretKey"] 
                ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
                ?? "test-secret-key-for-jwt-testing-minimum-256-bits-long";
            var issuer = jwtSettings["Issuer"] 
                ?? Environment.GetEnvironmentVariable("JWT_ISSUER") 
                ?? "test-issuer";
            var audience = jwtSettings["Audience"] 
                ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
                ?? "test-audience";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, employeeInfo.EmployeeId.ToString()),
                new Claim(ClaimTypes.Email, employeeInfo.Email),
                new Claim(ClaimTypes.Name, employeeInfo.FullName)
            };

            // Add role claims
            foreach (var role in employeeInfo.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<bool> ChangePasswordAsync(int employeeId, ChangePasswordRequest request)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"/api/auth/change-password/{employeeId}", content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<CreateEmployeeResponse?> CreateEmployeeAsync(CreateEmployeeRequest request)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/auth/create-employee", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    return JsonSerializer.Deserialize<CreateEmployeeResponse>(responseContent, options);
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<string> GetNextAvailableUidAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.GetAsync("/api/auth/next-uid");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return responseContent.Trim('"'); // Remove JSON quotes
                }

                // Fallback
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            }
            catch (Exception)
            {
                // Fallback
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            }
        }
    }
}
