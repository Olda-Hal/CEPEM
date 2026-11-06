using Microsoft.AspNetCore.Mvc;
using DatabaseAPI.Data;
using DatabaseAPI.Services;
using DatabaseAPI.Utils;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/examinations/{examinationId}/documents")]
public class ExaminationDocumentsController : ControllerBase
{
    private readonly ExaminationDocumentService _documentService;
    private readonly DatabaseContext _context;
    private readonly IEmployeeScopeService _employeeScopeService;
    private readonly ILogger<ExaminationDocumentsController> _logger;
    private readonly IAntivirusScanner _antivirusScanner;

    public ExaminationDocumentsController(
        ExaminationDocumentService documentService,
        DatabaseContext context,
        IEmployeeScopeService employeeScopeService,
        ILogger<ExaminationDocumentsController> logger,
        IAntivirusScanner? antivirusScanner = null)
    {
        _documentService = documentService;
        _context = context;
        _employeeScopeService = employeeScopeService;
        _logger = logger;
        _antivirusScanner = antivirusScanner ?? new NullAntivirusScanner();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(int examinationId)
    {
        try
        {
            if (!await CanAccessExaminationAsync(examinationId))
            {
                return NotFound(new { Error = "Examination not found" });
            }

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
            if (!await CanAccessExaminationAsync(examinationId))
            {
                return NotFound(new { Error = "Examination not found" });
            }

            if (file == null || file.Length == 0)
                return BadRequest(new { Error = "No file provided" });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var documentData = ms.ToArray();

            var validationError = FileSignatureValidator.ValidateExaminationDocument(documentData);
            if (validationError != null)
                return BadRequest(new { Error = validationError });

            var scan = await _antivirusScanner.ScanAsync(documentData);
            if (!scan.Clean)
            {
                return scan.ThreatName != null
                    ? BadRequest(new { Error = "File failed antivirus scan." })
                    : StatusCode(
                        StatusCodes.Status503ServiceUnavailable,
                        new { Error = scan.Error ?? "Antivirus scan is unavailable; the file was not stored." });
            }

            var document = await _documentService.SaveDocumentAsync(examinationId, documentData, file.FileName);

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
            if (!await CanAccessExaminationAsync(examinationId))
            {
                return NotFound(new { Error = "Examination not found" });
            }

            var result = await _documentService.GetDocumentDataAsync(documentId, examinationId);
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
            if (!await CanAccessExaminationAsync(examinationId))
            {
                return NotFound(new { Error = "Examination not found" });
            }

            var deleted = await _documentService.DeleteDocumentAsync(documentId, examinationId);
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

    private async Task<bool> CanAccessExaminationAsync(int examinationId)
    {
        var patientId = await _context.Examinations
            .Where(examination => examination.Id == examinationId)
            .Select(examination => (int?)examination.Event.PatientId)
            .FirstOrDefaultAsync();
        if (!patientId.HasValue)
        {
            return false;
        }

        var actor = await _employeeScopeService.GetActorScopeAsync();
        return await PatientScope
            .Apply(_context.Patients, actor)
            .AnyAsync(patient => patient.Id == patientId.Value && !patient.IsDeleted);
    }
}
