using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Utils;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

public class PatientPhotoService
{
    private readonly DatabaseContext _context;
    private readonly PhotoEncryptionService _encryptionService;
    private readonly ILogger<PatientPhotoService> _logger;
    private readonly string _photoStoragePath;

    public PatientPhotoService(
        DatabaseContext context,
        PhotoEncryptionService encryptionService,
        IConfiguration configuration,
        ILogger<PatientPhotoService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
        _photoStoragePath = configuration["PhotoStorage:Path"] ?? "/app/patient-photos";
        
        if (!Directory.Exists(_photoStoragePath))
        {
            Directory.CreateDirectory(_photoStoragePath);
            _logger.LogInformation("Created photo storage directory at {Path}", _photoStoragePath);
        }
    }

    public async Task<string> SavePatientPhotoAsync(int patientId, byte[] photoData)
    {
        var patient = await _context.Patients.FindAsync(patientId);
        if (patient == null)
        {
            throw new InvalidOperationException($"Patient with ID {patientId} not found");
        }

        var validationError = FileSignatureValidator.ValidatePatientPhoto(photoData);
        if (validationError != null)
        {
            throw new InvalidOperationException(validationError);
        }

        if (patient.PhotoPath != null)
        {
            await DeletePatientPhotoAsync(patientId);
        }

        var encryptedData = await _encryptionService.EncryptPhotoAsync(photoData);
        
        var fileName = $"patient_{patientId}_{Guid.NewGuid()}.enc";
        var filePath = Path.Combine(_photoStoragePath, fileName);

        await AtomicFileWriter.WriteAllBytesAsync(filePath, encryptedData);

        patient.PhotoPath = fileName;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Saved encrypted photo for patient {PatientId} at {Path}", patientId, fileName);

        return fileName;
    }

    public async Task<byte[]?> GetPatientPhotoAsync(int patientId)
    {
        var patient = await _context.Patients.FindAsync(patientId);
        if (patient?.PhotoPath == null)
        {
            return null;
        }

        var filePath = Path.Combine(_photoStoragePath, patient.PhotoPath);
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Photo file not found for patient {PatientId} at {Path}", patientId, filePath);
            return null;
        }

        var encryptedData = await File.ReadAllBytesAsync(filePath);
        var decryptedData = await _encryptionService.DecryptPhotoAsync(encryptedData);

        if (!_encryptionService.IsCurrentFormat(encryptedData))
        {
            await TryMigrateLegacyPhotoAsync(
                filePath, decryptedData, patientId, patient.PhotoPath);
        }

        return decryptedData;
    }

    /// <summary>
    /// Lazy migration: the first read of a CEPM1/raw-CBC photo rewrites it in
    /// the authenticated CEPM2 format. Failure never blocks serving the photo.
    /// </summary>
    private async Task TryMigrateLegacyPhotoAsync(
        string filePath, byte[] plaintext, int patientId, string fileName)
    {
        try
        {
            // Do not resurrect a photo deleted concurrently (e.g. GDPR purge).
            var stillReferenced = await _context.Patients
                .AnyAsync(patient => patient.Id == patientId && patient.PhotoPath == fileName);
            if (!stillReferenced || !File.Exists(filePath))
            {
                return;
            }

            var reEncrypted = await _encryptionService.EncryptPhotoAsync(plaintext);
            await AtomicFileWriter.WriteAllBytesAsync(filePath, reEncrypted);
            _logger.LogInformation("Re-encrypted legacy photo file {Path} with CEPM2", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not re-encrypt legacy photo file {Path}", filePath);
        }
    }

    public async Task<bool> DeletePatientPhotoAsync(int patientId)
    {
        var patient = await _context.Patients.FindAsync(patientId);
        if (patient?.PhotoPath == null)
        {
            return false;
        }

        var filePath = Path.Combine(_photoStoragePath, patient.PhotoPath);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Deleted photo file for patient {PatientId} at {Path}", patientId, filePath);
        }

        patient.PhotoPath = null;
        await _context.SaveChangesAsync();

        return true;
    }

    public string? GetPatientPhotoUrl(int patientId, Patient? patient = null)
    {
        if (patient != null && patient.PhotoPath != null)
        {
            return $"/api/patients/{patientId}/photo";
        }

        return null;
    }
}
