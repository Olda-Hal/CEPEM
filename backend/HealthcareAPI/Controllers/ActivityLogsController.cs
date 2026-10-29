using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HealthcareAPI.Attributes;
using HealthcareAPI.Middleware;

namespace HealthcareAPI.Controllers
{
    /// <summary>
    /// Authenticated proxy to DatabaseAPI's activity audit log. Keeps the
    /// internal service behind the service key while giving permitted
    /// employees list/export access from the browser.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ActivityLogsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ActivityLogsController> _logger;

        public ActivityLogsController(IHttpClientFactory httpClientFactory, ILogger<ActivityLogsController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [RequirePermission("audit.read")]
        [HttpGet]
        [PermissionDisplayName("View activity audit log")]
        public async Task<IActionResult> GetLogs()
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync($"/api/activitylogs{Request.QueryString}");
            var content = await response.Content.ReadAsStringAsync();
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json"
            };
        }

        [RequirePermission("audit.read")]
        [HttpGet("export")]
        [PermissionDisplayName("Export activity audit log")]
        public async Task<IActionResult> ExportCsv()
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync($"/api/activitylogs/export{Request.QueryString}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Activity log export failed with {StatusCode}", response.StatusCode);
                return StatusCode((int)response.StatusCode);
            }
            var bytes = await response.Content.ReadAsByteArrayAsync();
            response.Content.Headers.TryGetValues("Content-Disposition", out var dispositions);
            var fileName = dispositions?.FirstOrDefault()?.Split("filename=").LastOrDefault() ?? "activity-log.csv";
            return File(bytes, "text/csv", fileName.Trim('"'));
        }
    }
}
