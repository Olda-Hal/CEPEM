using System.Security.Claims;
using HealthcareAPI.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthcareAPI.Controllers;

/// <summary>
/// Permission-aware command-palette search. The browser may search patients,
/// visits and documents only with patients.read; evaluations with
/// evaluations.read; employees with employees.read. The internal DatabaseAPI
/// search endpoint returns only the requested categories.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        IPermissionEvaluator permissionEvaluator,
        IHttpClientFactory httpClientFactory,
        ILogger<SearchController> logger)
    {
        _permissionEvaluator = permissionEvaluator;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 5)
    {
        var employeeIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(employeeIdClaim, out var employeeId))
        {
            return Unauthorized();
        }

        limit = Math.Clamp(limit, 1, 20);
        var term = (q ?? string.Empty).Trim();
        if (term.Length < 2)
        {
            return Content("{\"query\":\"\",\"patients\":[],\"visits\":[],\"documents\":[],\"evaluations\":[],\"employees\":[]}", "application/json");
        }

        var canPatients = await HasPermissionAsync(employeeId, "patients.read");
        var canEvaluations = await HasPermissionAsync(employeeId, "evaluations.read");
        var canEmployees = await HasPermissionAsync(employeeId, "employees.read");

        if (!canPatients && !canEvaluations && !canEmployees)
        {
            return Content("{\"query\":\"\",\"patients\":[],\"visits\":[],\"documents\":[],\"evaluations\":[],\"employees\":[]}", "application/json");
        }

        var url = $"/api/search?q={Uri.EscapeDataString(term)}&limit={limit}" +
                  $"&patients={canPatients.ToString().ToLowerInvariant()}" +
                  $"&evaluations={canEvaluations.ToString().ToLowerInvariant()}" +
                  $"&employees={canEmployees.ToString().ToLowerInvariant()}";

        var client = _httpClientFactory.CreateClient("DatabaseAPI");
        var response = await client.GetAsync(url, HttpContext.RequestAborted);
        var content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Global search failed with status {StatusCode}", (int)response.StatusCode);
        }

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = content,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json"
        };
    }

    private async Task<bool> HasPermissionAsync(int employeeId, string permissionCode)
    {
        return await _permissionEvaluator.IsAllowedAsync(employeeId, permissionCode, HttpContext.RequestAborted) == true;
    }
}
