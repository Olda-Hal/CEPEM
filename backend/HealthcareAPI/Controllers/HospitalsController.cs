using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HealthcareAPI.Attributes;
using HealthcareAPI.Utils;
using System.Security.Claims;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HospitalsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HospitalsController> _logger;

    public HospitalsController(IHttpClientFactory httpClientFactory, ILogger<HospitalsController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    [PermissionDisplayName("List Hospitals")]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var actorCountryCode = User.FindFirst("country_code")?.Value;
            var actorCountryScopeId = CountryScopeMapper.ToScopeId(actorCountryCode);
            var isCountryAdmin = User.IsInRole("Country Admin") && !User.IsInRole("SysAdmin");
            var endpoint = isCountryAdmin
                ? $"/api/hospitals?countryScopeId={actorCountryScopeId}"
                : "/api/hospitals";

            var response = await client.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error retrieving hospitals");

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hospitals");
            return StatusCode(500, new { Error = "Error getting hospitals", Details = ex.Message });
        }
    }

    [HttpGet("{hospitalId}/examination-types")]
    [PermissionDisplayName("List Hospital Examination Types")]
    public async Task<IActionResult> GetHospitalExaminationTypes(int hospitalId, [FromQuery] string language = "en")
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync($"/api/hospitals/{hospitalId}/examination-types?language={Uri.EscapeDataString(language)}");

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Error retrieving hospital examination types");

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hospital examination types for hospital {HospitalId}", hospitalId);
            return StatusCode(500, new { Error = "Error getting hospital examination types", Details = ex.Message });
        }
    }

    [HttpPost]
    [PermissionDisplayName("Create Hospital")]
    public async Task<IActionResult> Create([FromBody] CreateHospitalRequest request)
    {
        try
        {
            var actorCountryCode = CountryScopeMapper.NormalizeCode(User.FindFirst("country_code")?.Value);
            var actorCountryScopeId = CountryScopeMapper.ToScopeId(actorCountryCode);
            var isCountryAdmin = User.IsInRole("Country Admin") && !User.IsInRole("SysAdmin");

            if (isCountryAdmin)
            {
                request.CountryScopeId = actorCountryScopeId;
                request.Country = actorCountryCode;
            }

            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("/api/hospitals", httpContent);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, error);
            }

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating hospital");
            return StatusCode(500, new { Error = "Error creating hospital", Details = ex.Message });
        }
    }

    [HttpPut("{hospitalId}")]
    [PermissionDisplayName("Update Hospital")]
    public async Task<IActionResult> Update(int hospitalId, [FromBody] UpdateHospitalRequest request)
    {
        try
        {
            var actorCountryCode = CountryScopeMapper.NormalizeCode(User.FindFirst("country_code")?.Value);
            var actorCountryScopeId = CountryScopeMapper.ToScopeId(actorCountryCode);
            var isCountryAdmin = User.IsInRole("Country Admin") && !User.IsInRole("SysAdmin");

            if (isCountryAdmin)
            {
                request.CountryScopeId = actorCountryScopeId;
                request.Country = actorCountryCode;
            }

            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PutAsync($"/api/hospitals/{hospitalId}", httpContent);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, error);
            }

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating hospital");
            return StatusCode(500, new { Error = "Error updating hospital", Details = ex.Message });
        }
    }

    [HttpDelete("{hospitalId}")]
    [PermissionDisplayName("Deactivate Hospital")]
    public async Task<IActionResult> Delete(int hospitalId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.DeleteAsync($"/api/hospitals/{hospitalId}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, error);
            }

            return Ok(new { Message = "Hospital deactivated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting hospital");
            return StatusCode(500, new { Error = "Error deleting hospital", Details = ex.Message });
        }
    }

    [HttpPut("{hospitalId}/examination-types")]
    [PermissionDisplayName("Set Hospital Examination Types")]
    public async Task<IActionResult> SetHospitalExaminationTypes(int hospitalId, [FromBody] int[] examinationTypeIds)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var httpContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(examinationTypeIds ?? Array.Empty<int>()),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PutAsync($"/api/hospitals/{hospitalId}/examination-types", httpContent);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, error);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting hospital examination types");
            return StatusCode(500, new { Error = "Error setting hospital examination types", Details = ex.Message });
        }
    }
}

public class CreateHospitalRequest
{
    public string? Name { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public int? CountryScopeId { get; set; }
}

public class UpdateHospitalRequest
{
    public string? Name { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public int? CountryScopeId { get; set; }
    public bool? Active { get; set; }
}
