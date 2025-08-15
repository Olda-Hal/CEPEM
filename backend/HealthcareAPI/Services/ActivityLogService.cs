using HealthcareAPI.Models;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace HealthcareAPI.Services
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly HttpClient _httpClient;
        private readonly string _databaseApiUrl;

        public ActivityLogService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _databaseApiUrl = configuration["DatabaseApiUrl"];
        }

        public async Task LogActivityAsync(ActivityLog log)
        {
            var content = new StringContent(JsonSerializer.Serialize(log), Encoding.UTF8, "application/json");
            await _httpClient.PostAsync($"{_databaseApiUrl}/api/activitylogs", content);
        }
    }
}
