using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HealthcareAPI.Services;
using System.Text.Json;

namespace HealthcareAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PatientsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PatientsController> _logger;

        public PatientsController(IHttpClientFactory httpClientFactory, ILogger<PatientsController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchPatients(
            [FromQuery] int page = 0,
            [FromQuery] int limit = 20,
            [FromQuery] string search = "",
            [FromQuery] string sortBy = "LastName",
            [FromQuery] string sortOrder = "asc")
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                
                var queryParams = new List<string>();
                queryParams.Add($"page={page}");
                queryParams.Add($"limit={limit}");
                queryParams.Add($"search={Uri.EscapeDataString(search)}");
                queryParams.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
                queryParams.Add($"sortOrder={Uri.EscapeDataString(sortOrder)}");
                
                var queryString = string.Join("&", queryParams);
                var response = await client.GetAsync($"/api/patients/search?{queryString}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var jsonDocument = JsonDocument.Parse(content);
                    return Ok(jsonDocument.RootElement);
                }

                return StatusCode((int)response.StatusCode, "Error retrieving patients");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching patients");
                return StatusCode(500, "An error occurred while searching patients");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPatient(int id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.GetAsync($"/api/patients/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var jsonDocument = JsonDocument.Parse(content);
                    return Ok(jsonDocument.RootElement);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound($"Patient with id {id} not found");
                }

                return StatusCode((int)response.StatusCode, "Error retrieving patient");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting patient {PatientId}", id);
                return StatusCode(500, "An error occurred while getting patient");
            }
        }
    }
}
