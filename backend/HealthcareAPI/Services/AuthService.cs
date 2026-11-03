using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
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
                var refreshToken = GenerateJwtToken(employeeInfo, isRefreshToken: true);
                var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenDays);

                // Check if password has expired
                var requiresPasswordChange = employeeInfo.PasswordExpiration.HasValue && 
                                           employeeInfo.PasswordExpiration.Value.Date <= DateTime.UtcNow.Date;

                return new LoginResponse
                {
                    Token = token,
                    RefreshToken = refreshToken,
                    RefreshTokenExpiresAt = refreshTokenExpiresAt,
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

        public async Task<ImpersonationResponse?> ImpersonateAsync(int targetEmployeeId, int impersonatorEmployeeId, string impersonatorName)
        {
            // Never allow a preview of your own account (and never nest a
            // preview inside another preview — the controller guards this too).
            if (targetEmployeeId == impersonatorEmployeeId)
            {
                return null;
            }

            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.GetAsync($"/api/employees/{targetEmployeeId}");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var employeeInfo = JsonSerializer.Deserialize<EmployeeAuthInfo>(responseContent, options);

                // Deactivated accounts are not reachable for preview either.
                if (employeeInfo == null || !employeeInfo.Active)
                {
                    return null;
                }

                // Preview tokens are intentionally short-lived (1 hour).
                var token = GenerateJwtToken(
                    employeeInfo,
                    impersonatorEmployeeId,
                    impersonatorName,
                    lifetimeMinutes: 60);

                return new ImpersonationResponse
                {
                    Token = token,
                    EmployeeId = employeeInfo.EmployeeId,
                    FullName = employeeInfo.FullName,
                    Email = employeeInfo.Email,
                    Roles = employeeInfo.Roles ?? new List<string>(),
                    ImpersonatorName = impersonatorName
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        private int AccessTokenMinutes =>
            Math.Max(5, _configuration.GetValue("JWT:AccessTokenMinutes", 30));

        private int RefreshTokenDays =>
            Math.Max(1, _configuration.GetValue("JWT:RefreshTokenDays", 7));

        private (string Key, string? Issuer, string? Audience) GetJwtSettings()
        {
            var jwtSettings = _configuration.GetSection("JWT");
            var secretKey = jwtSettings["SecretKey"]
                ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException(
                    "JWT secret key is not configured. Set the JWT__SecretKey configuration value or the JWT_SECRET_KEY environment variable.");
            }

            return (
                secretKey,
                jwtSettings["Issuer"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER"),
                jwtSettings["Audience"] ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE"));
        }

        private string GenerateJwtToken(
            EmployeeAuthInfo employeeInfo,
            int? impersonatorEmployeeId = null,
            string? impersonatorName = null,
            int? lifetimeMinutes = null,
            bool isRefreshToken = false)
        {
            var (secretKey, issuer, audience) = GetJwtSettings();

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, employeeInfo.EmployeeId.ToString()),
                new Claim(ClaimTypes.Email, employeeInfo.Email),
                new Claim(ClaimTypes.Name, employeeInfo.FullName),
                new Claim("acl_version", employeeInfo.AccessControlVersion.ToString()),
                new Claim("country_code", employeeInfo.CountryCode),
                new Claim("token_type", isRefreshToken ? "refresh" : "access")
            };

            // Add role claims
            foreach (var role in employeeInfo.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Preview ("náhled jako zaměstnanec"): the token still identifies
            // the target employee, but records who is really behind the wheel.
            if (impersonatorEmployeeId.HasValue)
            {
                claims.Add(new Claim("impersonator_id", impersonatorEmployeeId.Value.ToString()));
                claims.Add(new Claim("impersonator_name", impersonatorName ?? string.Empty));
            }

            var expires = isRefreshToken
                ? DateTime.UtcNow.AddDays(RefreshTokenDays)
                : DateTime.UtcNow.AddMinutes(lifetimeMinutes ?? AccessTokenMinutes);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expires,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<LoginResponse?> RefreshAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return null;
            }

            var principal = ValidateRefreshToken(refreshToken);
            if (principal == null)
            {
                return null;
            }

            var employeeIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            var aclClaim = principal.FindFirst("acl_version");
            if (employeeIdClaim == null ||
                !int.TryParse(employeeIdClaim.Value, out var employeeId) ||
                aclClaim == null ||
                !int.TryParse(aclClaim.Value, out var aclVersion))
            {
                return null;
            }

            if (!await IsTokenStillValidAsync(employeeId, aclVersion))
            {
                return null;
            }

            var employeeInfo = await FetchEmployeeInfoAsync(employeeId);
            if (employeeInfo == null || !employeeInfo.Active)
            {
                return null;
            }

            var requiresPasswordChange = employeeInfo.PasswordExpiration.HasValue &&
                                         employeeInfo.PasswordExpiration.Value.Date <= DateTime.UtcNow.Date;

            return new LoginResponse
            {
                Token = GenerateJwtToken(employeeInfo),
                RefreshToken = GenerateJwtToken(employeeInfo, isRefreshToken: true),
                RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenDays),
                Email = employeeInfo.Email,
                FullName = employeeInfo.FullName,
                PasswordExpiration = employeeInfo.PasswordExpiration,
                RequiresPasswordChange = requiresPasswordChange
            };
        }

        private ClaimsPrincipal? ValidateRefreshToken(string refreshToken)
        {
            try
            {
                var (secretKey, issuer, audience) = GetJwtSettings();
                var parameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
                    ValidIssuer = issuer,
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                var principal = new JwtSecurityTokenHandler().ValidateToken(
                    refreshToken,
                    parameters,
                    out var validatedToken);

                if (validatedToken is not JwtSecurityToken jwt ||
                    !string.Equals(jwt.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.Ordinal) ||
                    principal.FindFirst("token_type")?.Value != "refresh")
                {
                    return null;
                }

                return principal;
            }
            catch
            {
                return null;
            }
        }

        private async Task<bool> IsTokenStillValidAsync(int employeeId, int aclVersion)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.GetAsync(
                    $"/api/auth/token-valid?employeeId={employeeId}&accessControlVersion={aclVersion}");
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                var payload = await response.Content.ReadFromJsonAsync<TokenValidityPayload>();
                return payload?.Valid == true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<EmployeeAuthInfo?> FetchEmployeeInfoAsync(int employeeId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.GetAsync($"/api/employees/{employeeId}");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<EmployeeAuthInfo>();
            }
            catch
            {
                return null;
            }
        }

        private sealed class TokenValidityPayload
        {
            public bool Valid { get; set; }
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

        public async Task<(CreateEmployeeResponse? Result, string? ErrorBody)> CreateEmployeeAsync(CreateEmployeeRequest request)
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
                    return (JsonSerializer.Deserialize<CreateEmployeeResponse>(responseContent, options), null);
                }

                // Surface validation/scope errors from DatabaseAPI
                var body = (await response.Content.ReadAsStringAsync()).Trim().Trim('"');
                return (null, string.IsNullOrWhiteSpace(body) ? null : body);
            }
            catch (Exception)
            {
                return (null, null);
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
