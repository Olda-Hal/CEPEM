using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExaminationsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExaminationsController> _logger;

    public ExaminationsController(IHttpClientFactory httpClientFactory, ILogger<ExaminationsController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? language = "cs", [FromQuery] string? search = null,
        [FromQuery] int? hospitalId = null, [FromQuery] int? hospitalCountryScopeId = null,
        [FromQuery] string? patientCountryCode = null, [FromQuery] string? examinationCountryCode = null,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] int page = 0, [FromQuery] int limit = 50)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var query = BuildQueryString(language, search, hospitalId, hospitalCountryScopeId, patientCountryCode, examinationCountryCode, from, to, page, limit);
            var response = await client.GetAsync($"/api/examinations{query}");

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Error retrieving examinations");
            }

            var content = await response.Content.ReadAsStringAsync();
            return Ok(System.Text.Json.JsonSerializer.Deserialize<object>(content));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting examinations");
            return StatusCode(500, new { Error = "Error getting examinations" });
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string? language = "cs", [FromQuery] string? search = null,
        [FromQuery] int? hospitalId = null, [FromQuery] int? hospitalCountryScopeId = null,
        [FromQuery] string? patientCountryCode = null, [FromQuery] string? examinationCountryCode = null,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var query = BuildQueryString(language, search, hospitalId, hospitalCountryScopeId, patientCountryCode, examinationCountryCode, from, to, null, null);
            var response = await client.GetAsync($"/api/examinations/export{query}");

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Error exporting examinations");
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName
                ?? $"examinations_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";

            return File(content, "application/zip", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting examinations");
            return StatusCode(500, new { Error = "Error exporting examinations" });
        }
    }

    private static string BuildQueryString(string? language, string? search, int? hospitalId, int? hospitalCountryScopeId,
        string? patientCountryCode, string? examinationCountryCode, DateTime? from, DateTime? to, int? page, int? limit)
    {
        var query = new List<string>();

        Add(query, "language", language);
        Add(query, "search", search);
        Add(query, "hospitalId", hospitalId);
        Add(query, "hospitalCountryScopeId", hospitalCountryScopeId);
        Add(query, "patientCountryCode", patientCountryCode);
        Add(query, "examinationCountryCode", examinationCountryCode);
        Add(query, "from", from?.ToString("o"));
        Add(query, "to", to?.ToString("o"));
        Add(query, "page", page);
        Add(query, "limit", limit);

        if (query.Count == 0)
            return string.Empty;

        return "?" + string.Join("&", query);
    }

    private static void Add(List<string> query, string key, object? value)
    {
        if (value == null)
            return;

        var text = value.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return;

        query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(text)}");
    }
}
