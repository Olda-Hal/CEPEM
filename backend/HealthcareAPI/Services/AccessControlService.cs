using HealthcareAPI.Models;
using System.Text;
using System.Text.Json;

namespace HealthcareAPI.Services;

public interface IAccessControlService
{
    Task<AccessEvaluationResponse> EvaluateAsync(AccessEvaluationRequest request, CancellationToken cancellationToken = default);
    Task<EmployeePermissionRulesResponse?> GetEmployeeRulesAsync(int employeeId);
    Task<bool> UpdateEmployeeRulesAsync(int employeeId, UpdateEmployeePermissionRulesRequest request);
}

public class AccessControlService : IAccessControlService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AccessControlService> _logger;

    public AccessControlService(IHttpClientFactory httpClientFactory, ILogger<AccessControlService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("DatabaseAPI");
        _logger = logger;
    }

    public async Task<AccessEvaluationResponse> EvaluateAsync(AccessEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/access-control/evaluate", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new AccessEvaluationResponse { Allowed = false, Reason = $"Failed to evaluate access: {response.StatusCode}" };
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<AccessEvaluationResponse>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AccessEvaluationResponse { Allowed = false, Reason = "Empty ACL response" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating access control");
            return new AccessEvaluationResponse { Allowed = false, Reason = "Access evaluation error" };
        }
    }

    public async Task<EmployeePermissionRulesResponse?> GetEmployeeRulesAsync(int employeeId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/access-control/employees/{employeeId}/rules");
            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<EmployeePermissionRulesResponse>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching employee access rules");
            return null;
        }
    }

    public async Task<bool> UpdateEmployeeRulesAsync(int employeeId, UpdateEmployeePermissionRulesRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"/api/access-control/employees/{employeeId}/rules", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating employee access rules");
            return false;
        }
    }
}
