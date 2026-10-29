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

        public ActivityLogService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task LogActivityAsync(ActivityLog log)
        {
            var content = new StringContent(JsonSerializer.Serialize(log), Encoding.UTF8, "application/json");
            await _httpClient.PostAsync("api/activitylogs", content);
        }
    }
}
