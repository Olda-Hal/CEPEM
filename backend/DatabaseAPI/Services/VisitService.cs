using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

public interface IVisitService
{
    Task<List<VisitListItemDto>> GetVisitsAsync(int patientId);
    Task<VisitDetailDto?> GetVisitAsync(int patientId, int visitId);
    Task<VisitDetailDto?> CreateVisitAsync(int patientId, CreateVisitRequest request, int? createdByEmployeeId, string countryCode);
    Task<VisitDetailDto?> UpdateVisitAsync(int patientId, int visitId, UpdateVisitRequest request);
    Task<VisitDocumentDto?> AttachDocumentAsync(int patientId, int visitId, byte[] data, string originalFileName);
}

public class VisitService : IVisitService
{
    private readonly DatabaseContext _context;
    private readonly PatientDocumentService _patientDocumentService;

    public VisitService(DatabaseContext context, PatientDocumentService patientDocumentService)
    {
        _context = context;
        _patientDocumentService = patientDocumentService;
    }

    public async Task<List<VisitListItemDto>> GetVisitsAsync(int patientId)
    {
        return await _context.Visits
            .Where(v => v.PatientId == patientId)
            .OrderByDescending(v => v.VisitDate)
            .ThenByDescending(v => v.Id)
            .Select(v => new VisitListItemDto
            {
                Id = v.Id,
                VisitDate = v.VisitDate,
                Note = v.Note,
                DocumentCount = v.Documents.Count(),
                HasIntakeForm = v.SourceEventId != null && v.SourceEvent!.FormSubmissions.Any(),
                SourceEventId = v.SourceEventId
            })
            .ToListAsync();
    }

    public async Task<VisitDetailDto?> GetVisitAsync(int patientId, int visitId)
    {
        var visit = await _context.Visits
            .Include(v => v.Documents)
                .ThenInclude(d => d.PatientDocument)
            .Include(v => v.SourceEvent!)
                .ThenInclude(e => e.Comment)
            .FirstOrDefaultAsync(v => v.Id == visitId && v.PatientId == patientId);

        return visit == null ? null : MapDetail(visit);
    }

    public async Task<VisitDetailDto?> CreateVisitAsync(int patientId, CreateVisitRequest request, int? createdByEmployeeId, string countryCode)
    {
        var patientExists = await _context.Patients.AnyAsync(p => p.Id == patientId);
        if (!patientExists)
        {
            return null;
        }

        var visit = new Visit
        {
            PatientId = patientId,
            VisitDate = request.VisitDate.Date,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedByEmployeeId = createdByEmployeeId,
            CountryCode = countryCode
        };

        _context.Visits.Add(visit);
        await _context.SaveChangesAsync();

        return (await GetVisitAsync(patientId, visit.Id))!;
    }

    public async Task<VisitDetailDto?> UpdateVisitAsync(int patientId, int visitId, UpdateVisitRequest request)
    {
        var visit = await _context.Visits.FirstOrDefaultAsync(v => v.Id == visitId && v.PatientId == patientId);
        if (visit == null)
        {
            return null;
        }

        if (request.VisitDate.HasValue)
        {
            visit.VisitDate = request.VisitDate.Value.Date;
        }
        if (request.Note != null)
        {
            visit.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        }
        if (request.ClearSourceEvent)
        {
            visit.SourceEventId = null;
        }
        else if (request.SourceEventId.HasValue)
        {
            // Only allow linking events that belong to the same patient and carry a form submission
            var linkable = await _context.Events
                .AnyAsync(e => e.Id == request.SourceEventId.Value && e.PatientId == patientId);
            if (!linkable)
            {
                throw new ScopeViolationException("Event not found for this patient");
            }
            visit.SourceEventId = request.SourceEventId.Value;
        }

        await _context.SaveChangesAsync();
        return await GetVisitAsync(patientId, visitId);
    }

    public async Task<VisitDocumentDto?> AttachDocumentAsync(int patientId, int visitId, byte[] data, string originalFileName)
    {
        var visitExists = await _context.Visits.AnyAsync(v => v.Id == visitId && v.PatientId == patientId);
        if (!visitExists)
        {
            return null;
        }

        // Reuse the patient document pipeline (encryption, size limits, dedupe)
        var document = await _patientDocumentService.SavePatientDocumentAsync(patientId, data, originalFileName);

        var sortOrder = await _context.VisitDocuments
            .Where(vd => vd.VisitId == visitId)
            .Select(vd => (int?)vd.SortOrder)
            .MaxAsync() ?? 0;

        _context.VisitDocuments.Add(new VisitDocument
        {
            VisitId = visitId,
            PatientDocumentId = document.Id,
            SortOrder = sortOrder + 1
        });
        await _context.SaveChangesAsync();

        return new VisitDocumentDto
        {
            DocumentId = document.Id,
            FileName = document.FileName,
            OriginalFileName = document.OriginalFileName,
            UploadedAt = document.UploadedAt,
            FileSize = document.FileSize
        };
    }

    private static VisitDetailDto MapDetail(Visit visit) => new()
    {
        Id = visit.Id,
        PatientId = visit.PatientId,
        VisitDate = visit.VisitDate,
        Note = visit.Note,
        SourceEventId = visit.SourceEventId,
        Documents = visit.Documents
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Id)
            .Select(d => new VisitDocumentDto
            {
                DocumentId = d.PatientDocumentId,
                FileName = d.PatientDocument.FileName,
                OriginalFileName = d.PatientDocument.OriginalFileName,
                UploadedAt = d.PatientDocument.UploadedAt,
                FileSize = d.PatientDocument.FileSize
            })
            .ToList(),
        IntakeForm = visit.SourceEvent?.Comment?.Text
    };
}
