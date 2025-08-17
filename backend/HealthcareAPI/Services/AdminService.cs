using HealthcareAPI.Models;
using System.Text.Json;

namespace HealthcareAPI.Services
{
    public interface IAdminService
    {
        Task<List<EmployeeListItem>> GetAllEmployeesAsync();
        Task<EmployeeListItem?> GetEmployeeByIdAsync(int employeeId);
        Task<UpdateEmployeeResponse> UpdateEmployeeAsync(int employeeId, UpdateEmployeeRequest request);
        Task<bool> DeactivateEmployeeAsync(int employeeId);
        Task<List<RoleDto>> GetAllRolesAsync();
    }

    public class AdminService : IAdminService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AdminService> _logger;

        public AdminService(IHttpClientFactory httpClientFactory, ILogger<AdminService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("DatabaseAPI");
            _logger = logger;
        }

        public async Task<List<EmployeeListItem>> GetAllEmployeesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/admin/employees");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<EmployeeListItem>>(json, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    }) ?? new List<EmployeeListItem>();
                }
                _logger.LogError($"Failed to get employees: {response.StatusCode}");
                return new List<EmployeeListItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all employees");
                return new List<EmployeeListItem>();
            }
        }

        public async Task<EmployeeListItem?> GetEmployeeByIdAsync(int employeeId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/admin/employees/{employeeId}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<EmployeeListItem>(json, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting employee {employeeId}");
                return null;
            }
        }

        public async Task<UpdateEmployeeResponse> UpdateEmployeeAsync(int employeeId, UpdateEmployeeRequest request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PutAsync($"/api/admin/employees/{employeeId}", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<UpdateEmployeeResponse>(responseJson, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    }) ?? new UpdateEmployeeResponse { Success = false, Message = "Failed to deserialize response" };
                }
                
                var error = await response.Content.ReadAsStringAsync();
                return new UpdateEmployeeResponse 
                { 
                    Success = false, 
                    Message = $"Failed to update employee: {error}" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating employee {employeeId}");
                return new UpdateEmployeeResponse 
                { 
                    Success = false, 
                    Message = $"Error updating employee: {ex.Message}" 
                };
            }
        }

        public async Task<bool> DeactivateEmployeeAsync(int employeeId)
        {
            try
            {
                var response = await _httpClient.PatchAsync($"/api/admin/employees/{employeeId}/deactivate", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deactivating employee {employeeId}");
                return false;
            }
        }

        public async Task<List<RoleDto>> GetAllRolesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/admin/roles");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<RoleDto>>(json, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    }) ?? new List<RoleDto>();
                }
                _logger.LogError($"Failed to get roles: {response.StatusCode}");
                return new List<RoleDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all roles");
                return new List<RoleDto>();
            }
        }
    }
}
