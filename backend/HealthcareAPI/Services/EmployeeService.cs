using HealthcareAPI.Models;
using System.Text.Json;

namespace HealthcareAPI.Services
{
    public class EmployeeService : IEmployeeService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public EmployeeService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<Employee?> GetCurrentEmployeeAsync(int employeeId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.GetAsync($"/api/employees/{employeeId}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var employeeAuthInfo = JsonSerializer.Deserialize<EmployeeApiResponse>(responseContent, options);
                    
                    if (employeeAuthInfo != null)
                    {
                        return new Employee
                        {
                            Id = employeeAuthInfo.EmployeeId,
                            FirstName = employeeAuthInfo.FirstName,
                            LastName = employeeAuthInfo.LastName,
                            Email = employeeAuthInfo.Email,
                            TitleBefore = employeeAuthInfo.TitleBefore,
                            TitleAfter = employeeAuthInfo.TitleAfter,
                            UID = employeeAuthInfo.UID,
                            Active = employeeAuthInfo.Active,
                            LastLoginAt = employeeAuthInfo.LastLoginAt,
                            Roles = employeeAuthInfo.Roles ?? new List<string>()
                        };
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<object> GetDashboardStatsAsync(int employeeId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.GetAsync($"/api/employees/dashboard-stats/{employeeId}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    return JsonSerializer.Deserialize<object>(responseContent, options) ?? new object();
                }

                return new object();
            }
            catch (Exception)
            {
                return new object();
            }
        }
    }
}
