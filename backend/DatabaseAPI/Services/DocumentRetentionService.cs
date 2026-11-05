using DatabaseAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

public sealed class DocumentRetentionOptions
{
    public const string SectionName = "DocumentRetention";

    public bool Enabled { get; set; } = true;

    /// <summary>How long soft-deleted document rows/files are kept before purge.</summary>
    public int RetentionDays { get; set; } = 30;

    public int CleanupIntervalHours { get; set; } = 24;
}

/// <summary>
/// Removes soft-deleted patient/examination documents after the retention
/// window. Deleting a document already removes its encrypted file; this job
/// guarantees historical rows and any leftover files are purged too.
/// </summary>
public class DocumentRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentRetentionService> _logger;

    public DocumentRetentionService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DocumentRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _configuration
            .GetSection(DocumentRetentionOptions.SectionName)
            .Get<DocumentRetentionOptions>() ?? new DocumentRetentionOptions();
        if (!options.Enabled)
        {
            return;
        }

        var interval = TimeSpan.FromHours(Math.Clamp(options.CleanupIntervalHours, 1, 24 * 7));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                var storagePath = _configuration["DocumentStorage:Path"] ?? "/app/patient-documents";

                var purged = await RunOnceAsync(
                    context, storagePath, options.RetentionDays, stoppingToken);
                if (purged > 0)
                {
                    _logger.LogInformation(
                        "Document retention purged {Count} soft-deleted document(s)", purged);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Document retention cleanup failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>One cleanup pass; separated for deterministic tests.</summary>
    public static async Task<int> RunOnceAsync(
        DatabaseContext context,
        string documentStoragePath,
        int retentionDays,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, retentionDays));

        var patientDocuments = await context.PatientDocuments
            .Where(document => document.IsDeleted && document.UploadedAt < cutoff)
            .ToListAsync(cancellationToken);

        var examinationDocuments = await context.ExaminationDocuments
            .Where(document => document.IsDeleted && document.UploadedAt < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var document in patientDocuments)
        {
            DeleteFile(documentStoragePath, document.EncryptedPath);
        }
        foreach (var document in examinationDocuments)
        {
            DeleteFile(documentStoragePath, document.EncryptedPath);
        }

        context.PatientDocuments.RemoveRange(patientDocuments);
        context.ExaminationDocuments.RemoveRange(examinationDocuments);
        await context.SaveChangesAsync(cancellationToken);

        return patientDocuments.Count + examinationDocuments.Count;
    }

    private static void DeleteFile(string storagePath, string fileName)
    {
        var filePath = Path.Combine(storagePath, fileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
