using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.Services;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/examinations/{examinationId}/documents")]
public class ExaminationDocumentsController : ControllerBase
{
    private readonly ExaminationDocumentService _documentService;
    private readonly ILogger<ExaminationDocumentsController> _logger;

    public ExaminationDocumentsController(ExaminationDocumentService documentService, ILogger<ExaminationDocumentsController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(int examinationId)
    {
        try
        {
            var documents = await _documentService.GetDocumentsAsync(examinationId);
            return Ok(documents.Select(d => new
            {
                d.Id,
                d.OriginalFileName,
                d.FileSize,
                d.UploadedAt
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents for examination {ExaminationId}", examinationId);
            return StatusCode(500, new { Error = "Error getting documents" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Upload(int examinationId, IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { Error = "No file provided" });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var document = await _documentService.SaveDocumentAsync(examinationId, ms.ToArray(), file.FileName);

            return CreatedAtAction(nameof(Download), new { examinationId, documentId = document.Id }, new
            {
                document.Id,
                document.OriginalFileName,
                document.FileSize,
                document.UploadedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document for examination {ExaminationId}", examinationId);
            return StatusCode(500, new { Error = "Error uploading document" });
        }
    }

    [HttpGet("{documentId}")]
    public async Task<IActionResult> Download(int examinationId, int documentId)
    {
        try
        {
            var result = await _documentService.GetDocumentDataAsync(documentId);
            if (result == null)
                return NotFound(new { Error = "Document not found" });

            return File(result.Value.Data, "application/octet-stream", result.Value.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document {DocumentId}", documentId);
            return StatusCode(500, new { Error = "Error downloading document" });
        }
    }

    [HttpDelete("{documentId}")]
    public async Task<IActionResult> Delete(int examinationId, int documentId)
    {
        try
        {
            var deleted = await _documentService.DeleteDocumentAsync(documentId);
            if (!deleted)
                return NotFound(new { Error = "Document not found" });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId}", documentId);
            return StatusCode(500, new { Error = "Error deleting document" });
        }
    }
}
