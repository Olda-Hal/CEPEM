using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace HealthcareAPI.Controllers;

[ApiController]
[Route("api/examinations/{examinationId}/documents")]
[Authorize]
public class ExaminationDocumentsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExaminationDocumentsController> _logger;

    public ExaminationDocumentsController(IHttpClientFactory httpClientFactory, ILogger<ExaminationDocumentsController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("{documentId}")]
    public async Task<IActionResult> Download(int examinationId, int documentId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("DatabaseAPI");
            var response = await client.GetAsync($"/api/examinations/{examinationId}/documents/{documentId}");

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Error retrieving document");
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName
                ?? "document";

            return File(content, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading examination document {DocumentId} for examination {ExaminationId}", documentId, examinationId);
            return StatusCode(500, "An error occurred while downloading the document");
        }
    }
}
