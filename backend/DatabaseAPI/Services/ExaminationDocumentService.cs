using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

public class ExaminationDocumentService
{
    private readonly DatabaseContext _context;
    private readonly DocumentEncryptionService _encryptionService;
    private readonly ILogger<ExaminationDocumentService> _logger;
    private readonly string _documentStoragePath;
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public ExaminationDocumentService(
        DatabaseContext context,
        DocumentEncryptionService encryptionService,
        IConfiguration configuration,
        ILogger<ExaminationDocumentService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
        _documentStoragePath = configuration["DocumentStorage:Path"] ?? "/app/patient-documents";

        if (!Directory.Exists(_documentStoragePath))
            Directory.CreateDirectory(_documentStoragePath);
    }

    public async Task<ExaminationDocument> SaveDocumentAsync(int examinationId, byte[] documentData, string originalFileName)
    {
        var examination = await _context.Examinations.FindAsync(examinationId);
        if (examination == null)
            throw new InvalidOperationException($"Examination with ID {examinationId} not found");

        if (documentData.Length > MaxFileSizeBytes)
            throw new InvalidOperationException($"Document size must not exceed {MaxFileSizeBytes / 1024 / 1024}MB");

        var encryptedData = await _encryptionService.EncryptDocumentAsync(documentData);

        var fileName = $"examination_{examinationId}_{Guid.NewGuid()}.enc";
        var filePath = Path.Combine(_documentStoragePath, fileName);

        await File.WriteAllBytesAsync(filePath, encryptedData);

        var document = new ExaminationDocument
        {
            ExaminationId = examinationId,
            FileName = fileName,
            OriginalFileName = originalFileName,
            UploadedAt = DateTime.UtcNow,
            FileSize = documentData.Length,
            EncryptedPath = fileName
        };

        _context.ExaminationDocuments.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Saved document {DocumentId} for examination {ExaminationId}", document.Id, examinationId);

        return document;
    }

    public async Task<List<ExaminationDocument>> GetDocumentsAsync(int examinationId)
    {
        return await _context.ExaminationDocuments
            .Where(d => d.ExaminationId == examinationId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<(byte[] Data, string FileName)?> GetDocumentDataAsync(int documentId)
    {
        var document = await _context.ExaminationDocuments.FindAsync(documentId);
        if (document == null || document.IsDeleted)
            return null;

        var filePath = Path.Combine(_documentStoragePath, document.EncryptedPath);
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Document file not found for document {DocumentId} at {Path}", documentId, filePath);
            return null;
        }

        var encryptedData = await File.ReadAllBytesAsync(filePath);
        var decryptedData = await _encryptionService.DecryptDocumentAsync(encryptedData);

        return (decryptedData, document.OriginalFileName);
    }

    public async Task<bool> DeleteDocumentAsync(int documentId)
    {
        var document = await _context.ExaminationDocuments.FindAsync(documentId);
        if (document == null)
            return false;

        document.IsDeleted = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted document {DocumentId}", documentId);

        return true;
    }
}
