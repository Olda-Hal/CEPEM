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

[HttpPost]
        public async Task<IActionResult> CreatePatient([FromBody] object createPatientRequest)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                
                var json = System.Text.Json.JsonSerializer.Serialize(createPatientRequest);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await client.PostAsync("/api/patients", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var jsonDocument = JsonDocument.Parse(responseContent);
                    return Ok(jsonDocument.RootElement);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return BadRequest(errorContent);
                }

                return StatusCode((int)response.StatusCode, "Error creating patient");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating patient");
                return StatusCode(500, "An error occurred while creating patient");
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

        [HttpGet("{id}/detail")]
        public async Task<IActionResult> GetPatientDetail(int id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.GetAsync($"/api/patients/{id}/detail");

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

                return StatusCode((int)response.StatusCode, "Error retrieving patient detail");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting patient detail {PatientId}", id);
                return StatusCode(500, "An error occurred while getting patient detail");
            }
        }

        [HttpPost("{id}/photo")]
        public async Task<IActionResult> UploadPatientPhoto(int id, [FromForm] IFormFile photo)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                
                using var content = new MultipartFormDataContent();
                using var fileStream = photo.OpenReadStream();
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(photo.ContentType);
                content.Add(streamContent, "photo", photo.FileName);

                var response = await client.PostAsync($"/api/patients/{id}/photo", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var jsonDocument = JsonDocument.Parse(responseContent);
                    return Ok(jsonDocument.RootElement);
                }

                return StatusCode((int)response.StatusCode, "Error uploading patient photo");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while uploading photo for patient {PatientId}", id);
                return StatusCode(500, "An error occurred while uploading patient photo");
            }
        }

        [HttpGet("{id}/photo")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPatientPhoto(int id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.GetAsync($"/api/patients/{id}/photo");

                if (response.IsSuccessStatusCode)
                {
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    return File(imageBytes, contentType);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                return StatusCode((int)response.StatusCode, "Error retrieving patient photo");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting photo for patient {PatientId}", id);
                return StatusCode(500, "An error occurred while getting patient photo");
            }
        }

        [HttpDelete("{id}/photo")]
        public async Task<IActionResult> DeletePatientPhoto(int id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.DeleteAsync($"/api/patients/{id}/photo");

                if (response.IsSuccessStatusCode)
                {
                    return NoContent();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                return StatusCode((int)response.StatusCode, "Error deleting patient photo");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting photo for patient {PatientId}", id);
                return StatusCode(500, "An error occurred while deleting patient photo");
            }
        }

        [HttpPost("{id}/documents")]
        public async Task<IActionResult> UploadPatientDocument(int id, [FromForm] IFormFile document)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                
                using var content = new MultipartFormDataContent();
                using var fileContent = new StreamContent(document.OpenReadStream());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(document.ContentType);
                content.Add(fileContent, "document", document.FileName);

                var response = await client.PostAsync($"/api/patients/{id}/documents", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return Ok(JsonSerializer.Deserialize<object>(responseContent));
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, errorContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while uploading document for patient {PatientId}", id);
                return StatusCode(500, "An error occurred while uploading document");
            }
        }

        [HttpGet("{id}/documents")]
        public async Task<IActionResult> GetPatientDocuments(int id)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.GetAsync($"/api/patients/{id}/documents");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Ok(JsonSerializer.Deserialize<object>(content));
                }

                return StatusCode((int)response.StatusCode, "Error retrieving patient documents");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting documents for patient {PatientId}", id);
                return StatusCode(500, "An error occurred while getting patient documents");
            }
        }

        [HttpGet("{patientId}/documents/{documentId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPatientDocument(int patientId, int documentId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.GetAsync($"/api/patients/{patientId}/documents/{documentId}");

                if (response.IsSuccessStatusCode)
                {
                    var pdfBytes = await response.Content.ReadAsByteArrayAsync();
                    return File(pdfBytes, "application/pdf");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                return StatusCode((int)response.StatusCode, "Error retrieving document");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting document {DocumentId} for patient {PatientId}", documentId, patientId);
                return StatusCode(500, "An error occurred while getting document");
            }
        }

        [HttpDelete("{patientId}/documents/{documentId}")]
        public async Task<IActionResult> DeletePatientDocument(int patientId, int documentId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("DatabaseAPI");
                var response = await client.DeleteAsync($"/api/patients/{patientId}/documents/{documentId}");

                if (response.IsSuccessStatusCode)
                {
                    return NoContent();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                return StatusCode((int)response.StatusCode, "Error deleting document");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting document {DocumentId} for patient {PatientId}", documentId, patientId);
                return StatusCode(500, "An error occurred while deleting document");
            }
        }
    }
}
