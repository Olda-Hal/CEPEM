using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

public class PatientDocumentService
{
    private readonly DatabaseContext _context;
    private readonly DocumentEncryptionService _encryptionService;
    private readonly ILogger<PatientDocumentService> _logger;
    private readonly string _documentStoragePath;
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public PatientDocumentService(
        DatabaseContext context,
        DocumentEncryptionService encryptionService,
        IConfiguration configuration,
        ILogger<PatientDocumentService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
        _documentStoragePath = configuration["DocumentStorage:Path"] ?? "/app/patient-documents";
        
        if (!Directory.Exists(_documentStoragePath))
        {
            Directory.CreateDirectory(_documentStoragePath);
            _logger.LogInformation("Created document storage directory at {Path}", _documentStoragePath);
        }
    }

    public async Task<PatientDocument> SavePatientDocumentAsync(int patientId, byte[] documentData, string originalFileName)
    {
        var patient = await _context.Patients.FindAsync(patientId);
        if (patient == null)
        {
            throw new InvalidOperationException($"Patient with ID {patientId} not found");
        }

        if (documentData.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException($"Document size must not exceed {MaxFileSizeBytes / 1024 / 1024}MB");
        }

        var encryptedData = await _encryptionService.EncryptDocumentAsync(documentData);
        
        var fileName = $"patient_{patientId}_{Guid.NewGuid()}.enc";
        var filePath = Path.Combine(_documentStoragePath, fileName);

        await File.WriteAllBytesAsync(filePath, encryptedData);

        var document = new PatientDocument
        {
            PatientId = patientId,
            FileName = fileName,
            OriginalFileName = originalFileName,
            UploadedAt = DateTime.UtcNow,
            FileSize = documentData.Length,
            EncryptedPath = fileName
        };

        _context.PatientDocuments.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Saved encrypted document {DocumentId} for patient {PatientId} at {Path}", 
            document.Id, patientId, fileName);

        return document;
    }

    public async Task<List<PatientDocument>> GetPatientDocumentsAsync(int patientId)
    {
        return await _context.PatientDocuments
            .Where(d => d.PatientId == patientId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<byte[]?> GetDocumentDataAsync(int documentId)
    {
        var document = await _context.PatientDocuments.FindAsync(documentId);
        if (document == null || document.IsDeleted)
        {
            return null;
        }

        var filePath = Path.Combine(_documentStoragePath, document.EncryptedPath);
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Document file not found for document {DocumentId} at {Path}", documentId, filePath);
            return null;
        }

        var encryptedData = await File.ReadAllBytesAsync(filePath);
        var decryptedData = await _encryptionService.DecryptDocumentAsync(encryptedData);

        return decryptedData;
    }

    public async Task<bool> DeleteDocumentAsync(int documentId)
    {
        var document = await _context.PatientDocuments.FindAsync(documentId);
        if (document == null)
        {
            return false;
        }

        document.IsDeleted = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Soft deleted document {DocumentId}", documentId);

        return true;
    }

    public long GetMaxFileSizeBytes()
    {
        return MaxFileSizeBytes;
    }
}
