using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Repositories;

public interface IExaminationsRepository
{
    Task<ExaminationSearchResponse> SearchAsync(ExaminationSearchFilters filters, CancellationToken cancellationToken = default);
}

public class ExaminationsRepository : IExaminationsRepository
{
    private readonly DatabaseContext _context;

    public ExaminationsRepository(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<ExaminationSearchResponse> SearchAsync(ExaminationSearchFilters filters, CancellationToken cancellationToken = default)
    {
        var normalizedLanguage = NormalizeLanguage(filters.Language);
        var normalizedPatientCountryCode = NormalizeCountryCode(filters.PatientCountryCode);
        var normalizedExaminationCountryCode = NormalizeCountryCode(filters.ExaminationCountryCode);
        var page = filters.Page < 0 ? 0 : filters.Page;
        var limit = filters.Limit <= 0 ? 50 : Math.Min(filters.Limit, 500);

        var query = _context.Examinations
            .AsNoTracking()
            .Include(ex => ex.Event)
                .ThenInclude(ev => ev.Patient)
                    .ThenInclude(p => p.Person)
            .Include(ex => ex.ExaminationType)
                .ThenInclude(et => et.NameTranslation)
            .Include(ex => ex.Documents)
            .AsQueryable();

        if (filters.From.HasValue)
        {
            query = query.Where(ex => ex.Event.HappenedAt >= filters.From.Value);
        }

        if (filters.To.HasValue)
        {
            query = query.Where(ex => ex.Event.HappenedAt <= filters.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedPatientCountryCode))
        {
            query = query.Where(ex => ex.Event.Patient.CountryCode == normalizedPatientCountryCode);
        }

        if (!string.IsNullOrWhiteSpace(normalizedExaminationCountryCode))
        {
            query = query.Where(ex => ex.CountryCode == normalizedExaminationCountryCode);
        }

        if (filters.HospitalId.HasValue)
        {
            var hospitalId = filters.HospitalId.Value;
            query = query.Where(ex => _context.Appointments.Any(a =>
                a.PersonId == ex.Event.Patient.PersonId &&
                a.HospitalId == hospitalId &&
                a.StartTime <= ex.Event.HappenedAt &&
                a.EndTime >= ex.Event.HappenedAt));
        }

        if (filters.HospitalCountryScopeId.HasValue)
        {
            var hospitalCountryScopeId = filters.HospitalCountryScopeId.Value;
            query = query.Where(ex => _context.Appointments.Any(a =>
                a.PersonId == ex.Event.Patient.PersonId &&
                a.Hospital.CountryScopeId == hospitalCountryScopeId &&
                a.StartTime <= ex.Event.HappenedAt &&
                a.EndTime >= ex.Event.HappenedAt));
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var searchTerm = filters.Search.Trim();
            var searchPattern = $"%{searchTerm}%";
            query = query.Where(ex =>
                EF.Functions.Like(ex.Event.Patient.Person.FirstName, searchPattern) ||
                EF.Functions.Like(ex.Event.Patient.Person.LastName, searchPattern) ||
                EF.Functions.Like(ex.ExaminationType.NameTranslation!.EN, searchPattern) ||
                EF.Functions.Like(ex.ExaminationType.NameTranslation.CS ?? string.Empty, searchPattern) ||
                _context.Appointments.Any(a =>
                    a.PersonId == ex.Event.Patient.PersonId &&
                    a.StartTime <= ex.Event.HappenedAt &&
                    a.EndTime >= ex.Event.HappenedAt &&
                    EF.Functions.Like(a.Hospital.Name ?? string.Empty, searchPattern)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(ex => ex.Event.HappenedAt)
            .ThenByDescending(ex => ex.Id)
            .Skip(page * limit)
            .Take(limit)
            .Select(ex => new ExaminationListItemDto
            {
                Id = ex.Id,
                EventId = ex.EventId,
                HappenedAt = ex.Event.HappenedAt,
                HappenedTo = ex.Event.HappenedTo,
                ExaminationTypeId = ex.ExaminationTypeId,
                ExaminationTypeName = normalizedLanguage == "cs"
                    ? ex.ExaminationType.NameTranslation!.CS ?? ex.ExaminationType.NameTranslation.EN
                    : normalizedLanguage == "nl"
                        ? ex.ExaminationType.NameTranslation!.NL ?? ex.ExaminationType.NameTranslation.EN
                        : ex.ExaminationType.NameTranslation!.EN,
                PatientId = ex.Event.PatientId,
                PersonId = ex.Event.Patient.PersonId,
                PatientFirstName = ex.Event.Patient.Person.FirstName,
                PatientLastName = ex.Event.Patient.Person.LastName,
                PatientCountryCode = ex.Event.Patient.CountryCode,
                ExaminationCountryCode = ex.CountryCode,
                HospitalId = _context.Appointments
                    .Where(a => a.PersonId == ex.Event.Patient.PersonId && a.StartTime <= ex.Event.HappenedAt && a.EndTime >= ex.Event.HappenedAt)
                    .OrderByDescending(a => a.StartTime)
                    .Select(a => (int?)a.HospitalId)
                    .FirstOrDefault(),
                HospitalName = _context.Appointments
                    .Where(a => a.PersonId == ex.Event.Patient.PersonId && a.StartTime <= ex.Event.HappenedAt && a.EndTime >= ex.Event.HappenedAt)
                    .OrderByDescending(a => a.StartTime)
                    .Select(a => a.Hospital.Name)
                    .FirstOrDefault(),
                HospitalCountryScopeId = _context.Appointments
                    .Where(a => a.PersonId == ex.Event.Patient.PersonId && a.StartTime <= ex.Event.HappenedAt && a.EndTime >= ex.Event.HappenedAt)
                    .OrderByDescending(a => a.StartTime)
                    .Select(a => (int?)a.Hospital.CountryScopeId)
                    .FirstOrDefault(),
                DocumentCount = ex.Documents.Count(d => !d.IsDeleted),
                Documents = ex.Documents
                    .Where(d => !d.IsDeleted)
                    .OrderByDescending(d => d.UploadedAt)
                    .Select(d => new ExaminationDocumentItemDto
                    {
                        Id = d.Id,
                        FileName = d.OriginalFileName,
                        FileSize = d.FileSize,
                        UploadedAt = d.UploadedAt
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return new ExaminationSearchResponse
        {
            TotalCount = totalCount,
            Page = page,
            Limit = limit,
            Items = items
        };
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "cs";

        return language.Trim().ToLowerInvariant();
    }

    private static string? NormalizeCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return null;

        return countryCode.Trim().ToUpperInvariant();
    }
}
