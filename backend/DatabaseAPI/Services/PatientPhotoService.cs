using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
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

        if (patient.PhotoPath != null)
        {
            await DeletePatientPhotoAsync(patientId);
        }

        var encryptedData = await _encryptionService.EncryptPhotoAsync(photoData);
        
        var fileName = $"patient_{patientId}_{Guid.NewGuid()}.enc";
        var filePath = Path.Combine(_photoStoragePath, fileName);

        await File.WriteAllBytesAsync(filePath, encryptedData);

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

        return decryptedData;
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
