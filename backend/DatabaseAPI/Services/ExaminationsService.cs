using System.IO.Compression;
using System.Text.Json;
using DatabaseAPI.APIModels;
using DatabaseAPI.Repositories;

namespace DatabaseAPI.Services;

public interface IExaminationsService
{
    Task<ExaminationSearchResponse> SearchAsync(ExaminationSearchFilters filters, CancellationToken cancellationToken = default);
    Task<ExaminationExportResponse> ExportAsync(ExaminationSearchFilters filters, CancellationToken cancellationToken = default);
}

public class ExaminationsService : IExaminationsService
{
    private readonly IExaminationsRepository _repository;
    private readonly ExaminationDocumentService _documentService;

    public ExaminationsService(IExaminationsRepository repository, ExaminationDocumentService documentService)
    {
        _repository = repository;
        _documentService = documentService;
    }

    public Task<ExaminationSearchResponse> SearchAsync(ExaminationSearchFilters filters, CancellationToken cancellationToken = default)
    {
        return _repository.SearchAsync(filters, cancellationToken);
    }

    public async Task<ExaminationExportResponse> ExportAsync(ExaminationSearchFilters filters, CancellationToken cancellationToken = default)
    {
        var exportFilters = CloneFiltersForExport(filters);
        var searchResult = await _repository.SearchAsync(exportFilters, cancellationToken);

        var metadata = new ExaminationExportMetadata
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Filters = exportFilters,
            TotalExaminations = searchResult.Items.Count
        };

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var examination in searchResult.Items)
            {
                var examMetadata = new ExaminationExportItemMetadata
                {
                    ExaminationId = examination.Id,
                    EventId = examination.EventId,
                    HappenedAt = examination.HappenedAt,
                    ExaminationTypeName = examination.ExaminationTypeName,
                    PatientId = examination.PatientId,
                    PatientName = $"{examination.PatientFirstName} {examination.PatientLastName}".Trim(),
                    PatientCountryCode = examination.PatientCountryCode,
                    ExaminationCountryCode = examination.ExaminationCountryCode,
                    HospitalId = examination.HospitalId,
                    HospitalName = examination.HospitalName,
                    HospitalCountryScopeId = examination.HospitalCountryScopeId
                };

                foreach (var document in examination.Documents)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var documentData = await _documentService.GetDocumentDataAsync(document.Id);
                    if (documentData == null)
                        continue;

                    var safeFileName = MakeSafeFileName(documentData.Value.FileName);
                    var zipPath = $"examinations/{examination.Id}/doc_{document.Id}_{safeFileName}";

                    var entry = archive.CreateEntry(zipPath, CompressionLevel.Fastest);
                    await using (var entryStream = entry.Open())
                    {
                        await entryStream.WriteAsync(documentData.Value.Data, cancellationToken);
                    }

                    examMetadata.Files.Add(new ExaminationExportFileMetadata
                    {
                        DocumentId = document.Id,
                        OriginalFileName = document.FileName,
                        ZipPath = zipPath,
                        FileSize = document.FileSize,
                        UploadedAt = document.UploadedAt
                    });
                }

                metadata.Examinations.Add(examMetadata);
            }

            metadata.TotalFiles = metadata.Examinations.Sum(ex => ex.Files.Count);

            var metadataEntry = archive.CreateEntry("metadata.json", CompressionLevel.Fastest);
            await using var metadataStream = metadataEntry.Open();
            await JsonSerializer.SerializeAsync(metadataStream, metadata, new JsonSerializerOptions
            {
                WriteIndented = true
            }, cancellationToken);
        }

        var fileName = $"examinations_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
        return new ExaminationExportResponse
        {
            FileName = fileName,
            Content = memoryStream.ToArray()
        };
    }

    private static ExaminationSearchFilters CloneFiltersForExport(ExaminationSearchFilters source)
    {
        return new ExaminationSearchFilters
        {
            Language = source.Language,
            Search = source.Search,
            HospitalId = source.HospitalId,
            HospitalCountryScopeId = source.HospitalCountryScopeId,
            PatientCountryCode = source.PatientCountryCode,
            ExaminationCountryCode = source.ExaminationCountryCode,
            From = source.From,
            To = source.To,
            Page = 0,
            Limit = 5000
        };
    }

    private static string MakeSafeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        sanitized = sanitized.Replace('/', '_').Replace('\\', '_');

        return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized;
    }
}
