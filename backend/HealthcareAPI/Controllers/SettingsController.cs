using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace HealthcareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SettingsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(IHttpClientFactory httpClientFactory, ILogger<SettingsController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet("quick-preview")]
        public async Task<IActionResult> GetQuickPreviewSettings()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.GetAsync("/api/settings/quick-preview");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var result = JsonSerializer.Deserialize<object>(content, options);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("DatabaseAPI returned error: {StatusCode}", response.StatusCode);
                    return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting quick preview settings");
                return StatusCode(500, "An error occurred while getting quick preview settings");
            }
        }

        [HttpPut("quick-preview")]
        public async Task<IActionResult> UpdateQuickPreviewSettings([FromBody] object request)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var jsonContent = JsonSerializer.Serialize(request);
                var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                var response = await client.PutAsync("/api/settings/quick-preview", httpContent);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var result = JsonSerializer.Deserialize<object>(content, options);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("DatabaseAPI returned error: {StatusCode}", response.StatusCode);
                    return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating quick preview settings");
                return StatusCode(500, "An error occurred while updating quick preview settings");
            }
        }
    }
}
