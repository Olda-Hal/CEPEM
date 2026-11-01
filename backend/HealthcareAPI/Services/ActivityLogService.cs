using HealthcareAPI.Models;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HealthcareAPI.Services
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ActivityLogService> _logger;

        public ActivityLogService(HttpClient httpClient, ILogger<ActivityLogService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task LogActivityAsync(ActivityLog log)
        {
            var content = new StringContent(JsonSerializer.Serialize(log), Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/activitylogs", content);
            if (!response.IsSuccessStatusCode)
            {
                // Never let audit failures break the request, but make the
                // missing service key/DB outage visible in the logs.
                _logger.LogWarning(
                    "Activity log write failed with {StatusCode} for {Action}",
                    (int)response.StatusCode, log.Action);
            }
        }
    }
}
