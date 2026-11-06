using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.Services;
using DatabaseAPI.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers;

/// <summary>
/// Internal aggregated search used by the gateway's command palette. Category
/// filtering by the caller's permissions happens in HealthcareAPI; this
/// endpoint is not exposed as a public YARP route.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private const int MaxLimit = 20;

    private readonly DatabaseContext _context;
    private readonly IEmployeeScopeService _employeeScopeService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        DatabaseContext context,
        IEmployeeScopeService employeeScopeService,
        ILogger<SearchController> logger)
    {
        _context = context;
        _employeeScopeService = employeeScopeService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<GlobalSearchResponse>> Search(
        [FromQuery] string? q,
        [FromQuery] int limit = 5,
        [FromQuery] bool patients = true,
        [FromQuery] bool evaluations = true,
        [FromQuery] bool employees = true)
    {
        try
        {
            var term = (q ?? string.Empty).Trim();
            var response = new GlobalSearchResponse { Query = term };
            if (term.Length < 2)
            {
                return Ok(response);
            }

            limit = Math.Clamp(limit, 1, MaxLimit);

            if (patients)
            {
                response.Patients = await SearchPatientsAsync(term, limit);
                response.Visits = await SearchVisitsAsync(term, limit);
                response.Documents = await SearchDocumentsAsync(term, limit);
            }

            if (evaluations)
            {
                response.Evaluations = await SearchEvaluationsAsync(term, limit);
            }

            if (employees)
            {
                response.Employees = await SearchEmployeesAsync(term, limit);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Global search failed for query {Query}", q);
            return StatusCode(500, "An error occurred while searching");
        }
    }

    private async Task<List<SearchPatientDto>> SearchPatientsAsync(string term, int limit)
    {
        var actor = await _employeeScopeService.GetActorScopeAsync();
        return await DatabaseAPI.Utils.PatientScope
            .Apply(_context.Patients.AsNoTracking(), actor)
            .Where(patient => !patient.IsDeleted)
            .Where(patient =>
                patient.Person.FirstName.Contains(term) ||
                patient.Person.LastName.Contains(term) ||
                patient.Person.UID.Contains(term) ||
                patient.Person.ContactToObjects.Any(link =>
                    link.Contact.Emails.Any(email => email.Email.Contains(term))) ||
                patient.Person.ContactToObjects.Any(link =>
                    link.Contact.PhoneNumbers.Any(phone => phone.PhoneNumber.Contains(term))))
            .OrderBy(patient => patient.Person.LastName)
            .ThenBy(patient => patient.Person.FirstName)
            .Take(limit)
            .Select(patient => new SearchPatientDto
            {
                Id = patient.Id,
                Uid = patient.Person.UID,
                FirstName = patient.Person.FirstName,
                LastName = patient.Person.LastName,
                BirthDate = patient.BirthDate,
                Email = patient.Person.ContactToObjects
                    .SelectMany(link => link.Contact.Emails)
                    .Select(email => email.Email)
                    .FirstOrDefault(),
                PhoneNumber = patient.Person.ContactToObjects
                    .SelectMany(link => link.Contact.PhoneNumbers)
                    .Select(phone => phone.PhoneNumber)
                    .FirstOrDefault()
            })
            .ToListAsync();
    }

    private async Task<List<SearchVisitDto>> SearchVisitsAsync(string term, int limit)
    {
        var actor = await _employeeScopeService.GetActorScopeAsync();
        var rows = await DatabaseAPI.Utils.PatientScope
            .ApplyToVisits(_context.Visits.AsNoTracking(), actor)
            .Where(visit => !visit.Patient.IsDeleted)
            .Where(visit =>
                (visit.Note != null && visit.Note.Contains(term)) ||
                visit.Patient.Person.FirstName.Contains(term) ||
                visit.Patient.Person.LastName.Contains(term))
            .OrderByDescending(visit => visit.VisitDate)
            .ThenByDescending(visit => visit.Id)
            .Take(limit)
            .Select(visit => new SearchVisitDto
            {
                Id = visit.Id,
                PatientId = visit.PatientId,
                PatientName = (visit.Patient.Person.FirstName + " " + visit.Patient.Person.LastName).Trim(),
                VisitDate = visit.VisitDate,
                Note = visit.Note,
                HasIntakeForm = visit.SourceEventId != null && visit.SourceEvent!.FormSubmissions.Any()
            })
            .ToListAsync();

        foreach (var row in rows)
        {
            row.Note = Truncate(row.Note, 160);
        }

        return rows;
    }

    private async Task<List<SearchDocumentDto>> SearchDocumentsAsync(string term, int limit)
    {
        var actor = await _employeeScopeService.GetActorScopeAsync();
        var visiblePatientIds = DatabaseAPI.Utils.PatientScope
            .Apply(_context.Patients, actor)
            .Select(patient => patient.Id);
        var rows = await _context.PatientDocuments
            .AsNoTracking()
            .Where(document => !document.IsDeleted && !document.Patient.IsDeleted)
            .Where(document => visiblePatientIds.Contains(document.PatientId))
            .Where(document =>
                document.OriginalFileName.Contains(term) ||
                document.FileName.Contains(term) ||
                (document.ExtractedText != null && document.ExtractedText.Contains(term)))
            .OrderByDescending(document => document.UploadedAt)
            .Take(limit)
            .Select(document => new
            {
                document.Id,
                document.PatientId,
                PatientName = (document.Patient.Person.FirstName + " " + document.Patient.Person.LastName).Trim(),
                document.OriginalFileName,
                document.UploadedAt,
                document.ExtractedText
            })
            .ToListAsync();

        return rows.Select(row => new SearchDocumentDto
        {
            DocumentId = row.Id,
            PatientId = row.PatientId,
            PatientName = row.PatientName,
            FileName = row.OriginalFileName,
            UploadedAt = row.UploadedAt,
            Snippet = BuildSnippet(row.ExtractedText, term)
        }).ToList();
    }

    private static string? BuildSnippet(string? text, string term)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var start = Math.Max(0, index - 60);
        var length = Math.Min(text.Length - start, term.Length + 120);
        var snippet = text.Substring(start, length).Replace('\n', ' ').Trim();
        return $"{(start > 0 ? "…" : string.Empty)}{snippet}{(start + length < text.Length ? "…" : string.Empty)}";
    }

    private async Task<List<SearchEvaluationDto>> SearchEvaluationsAsync(string term, int limit)
    {
        var actor = await _employeeScopeService.GetActorScopeAsync();
        var visiblePatientIds = DatabaseAPI.Utils.PatientScope
            .Apply(_context.Patients, actor)
            .Select(patient => patient.Id);

        var rows = await _context.EvaluationCases
            .AsNoTracking()
            .Include(evaluation => evaluation.Patient)
            .Include(evaluation => evaluation.Reviews)
            .Where(evaluation => evaluation.PatientId == null ||
                visiblePatientIds.Contains(evaluation.PatientId.Value))
            .Where(evaluation =>
                evaluation.Title.Contains(term) ||
                (evaluation.SourceSide != null && evaluation.SourceSide.Contains(term)) ||
                (evaluation.SourcePosture != null && evaluation.SourcePosture.Contains(term)))
            .OrderByDescending(evaluation => evaluation.CreatedAtUtc)
            .Take(limit * 4)
            .ToListAsync();

        return rows
            .Take(limit)
            .Select(evaluation => new SearchEvaluationDto
            {
                CaseId = evaluation.Id,
                Title = evaluation.Title,
                Side = evaluation.SourceSide,
                Posture = evaluation.SourcePosture,
                ReviewCount = evaluation.Reviews.Count,
                CreatedAtUtc = evaluation.CreatedAtUtc
            })
            .ToList();
    }

    private async Task<List<SearchEmployeeDto>> SearchEmployeesAsync(string term, int limit)
    {
        var visibleIds = await _employeeScopeService.GetVisibleEmployeeIdsAsync();
        if (visibleIds is { Count: 0 })
        {
            return new List<SearchEmployeeDto>();
        }

        var query = _context.Employees
            .AsNoTracking()
            .Include(employee => employee.Person)
                .ThenInclude(person => person.ContactToObjects)
                    .ThenInclude(link => link.Contact)
                        .ThenInclude(contact => contact.Emails)
            .Include(employee => employee.Person)
                .ThenInclude(person => person.UserRoles)
                    .ThenInclude(userRole => userRole.Role)
                        .ThenInclude(role => role.NameTranslation)
            .Where(employee => employee.Person.Active)
            .Where(employee =>
                employee.Person.FirstName.Contains(term) ||
                employee.Person.LastName.Contains(term) ||
                employee.Person.ContactToObjects.Any(link =>
                    link.Contact.Emails.Any(email => email.Email.Contains(term))));

        if (visibleIds != null)
        {
            query = query.Where(employee => visibleIds.Contains(employee.Id));
        }

        var rows = await query
            .OrderBy(employee => employee.Person.LastName)
            .ThenBy(employee => employee.Person.FirstName)
            .Take(limit)
            .ToListAsync();

        return rows.Select(employee => new SearchEmployeeDto
        {
            Id = employee.Id,
            FullName = $"{employee.Person.FirstName} {employee.Person.LastName}".Trim(),
            Email = employee.Person.ContactToObjects
                .SelectMany(link => link.Contact.Emails)
                .Select(email => email.Email)
                .FirstOrDefault(),
            Active = employee.Person.Active,
            Roles = employee.Person.UserRoles
                .Select(userRole => userRole.Role.NameTranslation?.EN)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList()
        }).ToList();
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
