using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

public sealed class GdprExportFile
{
    public string FileName { get; init; } = string.Empty;
    public byte[] Data { get; init; } = Array.Empty<byte>();
}

public sealed class GdprExportBundle
{
    public object Export { get; init; } = new();
    public List<GdprExportFile> Files { get; init; } = new();
}

public interface IGdprService
{
    Task<GdprDataMapDto?> GetDataMapAsync(int patientId, CancellationToken cancellationToken = default);
    Task<GdprExportBundle?> BuildExportBundleAsync(int patientId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error, object? Result)> AnonymizeAsync(
        int patientId, string confirmation, int? requestedByEmployeeId, CancellationToken cancellationToken = default);
}

public class GdprService : IGdprService
{
    private const string AnonymizedEmail = "anonymized@invalid";

    private readonly DatabaseContext _context;
    private readonly PatientDocumentService _documentService;
    private readonly PatientPhotoService _photoService;
    private readonly ExaminationDocumentService _examinationDocumentService;
    private readonly ILogger<GdprService> _logger;

    public GdprService(
        DatabaseContext context,
        PatientDocumentService documentService,
        PatientPhotoService photoService,
        ExaminationDocumentService examinationDocumentService,
        ILogger<GdprService> logger)
    {
        _context = context;
        _documentService = documentService;
        _photoService = photoService;
        _examinationDocumentService = examinationDocumentService;
        _logger = logger;
    }

    public async Task<GdprDataMapDto?> GetDataMapAsync(int patientId, CancellationToken cancellationToken = default)
    {
        var patient = await _context.Patients
            .AsNoTracking()
            .Include(p => p.Person)
            .FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient == null)
        {
            return null;
        }

        var visits = await _context.Visits
            .Where(v => v.PatientId == patientId)
            .Select(v => (DateTime?)v.VisitDate)
            .ToListAsync(cancellationToken);
        var events = await _context.Events
            .Where(e => e.PatientId == patientId)
            .Select(e => (DateTime?)e.HappenedAt)
            .ToListAsync(cancellationToken);
        var submissions = await _context.FormSubmissions
            .Where(fs => fs.PatientId == patientId)
            .Select(fs => (DateTime?)fs.SubmittedAtUtc)
            .ToListAsync(cancellationToken);
        var documents = await _context.PatientDocuments
            .Where(d => d.PatientId == patientId)
            .Select(d => (DateTime?)d.UploadedAt)
            .ToListAsync(cancellationToken);

        var lastActivity = visits.Concat(events).Concat(submissions).Concat(documents)
            .Where(value => value.HasValue)
            .Max();

        return new GdprDataMapDto
        {
            PatientId = patient.Id,
            PatientName = $"{patient.Person.FirstName} {patient.Person.LastName}".Trim(),
            Uid = patient.Person.UID,
            Visits = visits.Count,
            Documents = documents.Count,
            Photos = patient.PhotoPath == null ? 0 : 1,
            Events = events.Count,
            FormSubmissions = submissions.Count,
            EvaluationCases = await _context.EvaluationCases.CountAsync(c => c.PatientId == patientId, cancellationToken),
            Consents = await _context.PatientConsents.CountAsync(c => c.PatientId == patientId, cancellationToken),
            EmailLogs = await _context.EmailLogs.CountAsync(l => l.PersonId == patient.PersonId, cancellationToken),
            LastActivityAtUtc = lastActivity
        };
    }

    public async Task<GdprExportBundle?> BuildExportBundleAsync(int patientId, CancellationToken cancellationToken = default)
    {
        var patient = await _context.Patients
            .AsNoTracking()
            .Include(p => p.Person)
                .ThenInclude(person => person.Address)
            .Include(p => p.Person)
                .ThenInclude(person => person.ContactToObjects)
                    .ThenInclude(link => link.Contact)
                        .ThenInclude(contact => contact.Emails)
            .Include(p => p.Person)
                .ThenInclude(person => person.ContactToObjects)
                    .ThenInclude(link => link.Contact)
                        .ThenInclude(contact => contact.PhoneNumbers)
            .Include(p => p.Person)
                .ThenInclude(person => person.EmailHistories)
            .Include(p => p.Person)
                .ThenInclude(person => person.PhoneNumberHistories)
            .Include(p => p.Comment)
            .Include(p => p.Events)
                .ThenInclude(e => e.EventType)
                    .ThenInclude(type => type.NameTranslation)
            .Include(p => p.Events)
                .ThenInclude(e => e.Comment)
            .Include(p => p.FormSubmissions)
                .ThenInclude(fs => fs.Medication)
            .Include(p => p.FormSubmissions)
                .ThenInclude(fs => fs.Lifestyle)
            .Include(p => p.FormSubmissions)
                .ThenInclude(fs => fs.ReproductiveHealth)
            .Include(p => p.FormSubmissions)
                .ThenInclude(fs => fs.Consent)
            .Include(p => p.FormSubmissions)
                .ThenInclude(fs => fs.SicknessHistories)
            .FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
        if (patient == null)
        {
            return null;
        }

        var visits = await _context.Visits
            .AsNoTracking()
            .Where(v => v.PatientId == patientId)
            .Include(v => v.Documents)
                .ThenInclude(d => d.PatientDocument)
            .OrderBy(v => v.VisitDate)
            .ToListAsync(cancellationToken);

        var documents = await _context.PatientDocuments
            .AsNoTracking()
            .Where(d => d.PatientId == patientId)
            .OrderBy(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        var evaluations = await _context.EvaluationCases
            .AsNoTracking()
            .Where(c => c.PatientId == patientId)
            .Include(c => c.Reviews)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var consents = await _context.PatientConsents
            .AsNoTracking()
            .Where(c => c.PatientId == patientId)
            .OrderBy(c => c.AcceptedAtUtc)
            .ToListAsync(cancellationToken);

        var emailLogs = await _context.EmailLogs
            .AsNoTracking()
            .Where(l => l.PersonId == patient.PersonId)
            .OrderBy(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var emails = patient.Person.ContactToObjects
            .SelectMany(link => link.Contact.Emails)
            .Select(email => email.Email)
            .ToList();
        var phones = patient.Person.ContactToObjects
            .SelectMany(link => link.Contact.PhoneNumbers)
            .Select(phone => phone.PhoneNumber)
            .ToList();

        var export = new
        {
            exportedAtUtc = DateTime.UtcNow,
            patient = new
            {
                patient.Id,
                patient.PersonId,
                patient.BirthDate,
                patient.InsuranceNumber,
                patient.Alive,
                patient.CountryCode,
                patient.HomeHospitalId,
                patient.IsDeleted,
                patient.DeletedAtUtc,
                patient.CreatedAt,
                comment = patient.Comment?.Text
            },
            person = new
            {
                patient.Person.FirstName,
                patient.Person.LastName,
                patient.Person.TitleBefore,
                patient.Person.TitleAfter,
                patient.Person.UID,
                patient.Person.Gender,
                patient.Person.CountryCode,
                patient.Person.CreatedAt,
                patient.Person.Active,
                address = patient.Person.Address == null
                    ? null
                    : new
                    {
                        patient.Person.Address.Street,
                        patient.Person.Address.City,
                        patient.Person.Address.PostalCode,
                        patient.Person.Address.Country
                    },
                emails,
                phones,
                emailHistory = patient.Person.EmailHistories.Select(history => new
                {
                    history.Email,
                    history.UsedFrom,
                    history.UsedTo
                }),
                phoneHistory = patient.Person.PhoneNumberHistories.Select(history => new
                {
                    history.PhoneNumber,
                    history.UsedFrom,
                    history.UsedTo
                })
            },
            visits = visits.Select(visit => new
            {
                visit.Id,
                visit.VisitDate,
                visit.Note,
                visit.SourceEventId,
                documents = visit.Documents.Select(link => new
                {
                    link.PatientDocument.Id,
                    link.PatientDocument.OriginalFileName,
                    link.PatientDocument.UploadedAt,
                    link.PatientDocument.FileSize,
                    link.PatientDocument.IsDeleted
                })
            }),
            documents = documents.Select(document => new
            {
                document.Id,
                document.OriginalFileName,
                document.UploadedAt,
                document.FileSize,
                document.IsDeleted
            }),
            events = patient.Events
                .OrderBy(e => e.HappenedAt)
                .Select(e => new
                {
                    e.Id,
                    type = e.EventType?.NameTranslation?.EN ?? e.EventType?.NameTranslation?.CS,
                    e.HappenedAt,
                    comment = e.Comment?.Text
                }),
            formSubmissions = patient.FormSubmissions
                .OrderBy(fs => fs.SubmittedAtUtc)
                .Select(fs => new
                {
                    fs.Id,
                    fs.SubmittedAtUtc,
                    medication = fs.Medication == null ? null : new
                    {
                        fs.Medication.MedBloodPressure,
                        fs.Medication.MedHeart,
                        fs.Medication.MedCholesterol,
                        fs.Medication.MedBloodThinners,
                        fs.Medication.MedDiabetes,
                        fs.Medication.MedThyroid,
                        fs.Medication.MedNerves,
                        fs.Medication.MedPsych,
                        fs.Medication.MedDigestion,
                        fs.Medication.MedPain,
                        fs.Medication.MedDehydration,
                        fs.Medication.MedBreathing,
                        fs.Medication.MedAntibiotics,
                        fs.Medication.MedSupplements,
                        fs.Medication.MedAllergies
                    },
                    lifestyle = fs.Lifestyle == null ? null : new
                    {
                        fs.Lifestyle.PoorSleep,
                        fs.Lifestyle.DigestiveIssues,
                        fs.Lifestyle.PhysicalStress,
                        fs.Lifestyle.MentalStress,
                        fs.Lifestyle.Smoking,
                        fs.Lifestyle.Fatigue,
                        fs.Lifestyle.LastMealHours,
                        fs.Lifestyle.VaccinesAfter2023,
                        fs.Lifestyle.HadCovid,
                        fs.Lifestyle.CovidWhen,
                        fs.Lifestyle.CovidVaccine,
                        fs.Lifestyle.Weight,
                        fs.Lifestyle.Height,
                        fs.Lifestyle.AdditionalHealthInfo
                    },
                    reproductiveHealth = fs.ReproductiveHealth == null ? null : new
                    {
                        fs.ReproductiveHealth.LastMenstruationDate,
                        fs.ReproductiveHealth.MenstruationCycleDays,
                        fs.ReproductiveHealth.YearsSinceLastMenstruation,
                        fs.ReproductiveHealth.GaveBirth,
                        fs.ReproductiveHealth.BirthCount,
                        fs.ReproductiveHealth.BirthWhen,
                        fs.ReproductiveHealth.Breastfed,
                        fs.ReproductiveHealth.BreastfeedingMonths,
                        fs.ReproductiveHealth.BreastfeedingInflammation,
                        fs.ReproductiveHealth.EndedWithInflammation,
                        fs.ReproductiveHealth.Contraception,
                        fs.ReproductiveHealth.ContraceptionDuration,
                        fs.ReproductiveHealth.Estrogen,
                        fs.ReproductiveHealth.EstrogenType,
                        fs.ReproductiveHealth.Interruption,
                        fs.ReproductiveHealth.InterruptionCount,
                        fs.ReproductiveHealth.Miscarriage,
                        fs.ReproductiveHealth.MiscarriageCount,
                        fs.ReproductiveHealth.BreastInjury,
                        fs.ReproductiveHealth.Mammogram,
                        fs.ReproductiveHealth.MammogramCount,
                        fs.ReproductiveHealth.BreastBiopsy,
                        fs.ReproductiveHealth.BreastImplants,
                        fs.ReproductiveHealth.BreastSurgery,
                        fs.ReproductiveHealth.BreastSurgeryType,
                        fs.ReproductiveHealth.FamilyTumors,
                        fs.ReproductiveHealth.FamilyTumorType
                    },
                    consent = fs.Consent == null ? null : new
                    {
                        fs.Consent.ConfirmAccuracy,
                        fs.Consent.TermsAccepted,
                        fs.Consent.SignaturePlace,
                        fs.Consent.SignatureDate,
                        fs.Consent.SignatureVector
                    },
                    sicknessHistory = fs.SicknessHistories.Select(sickness => new
                    {
                        sickness.SicknessName,
                        sickness.HadSickness,
                        sickness.SicknessWhen,
                        sickness.Vaccinated,
                        sickness.VaccinationWhen,
                        sickness.Notes
                    })
                }),
            evaluations = evaluations.Select(evaluation => new
            {
                evaluation.Id,
                evaluation.Title,
                evaluation.SourceSide,
                evaluation.SourcePosture,
                evaluation.SourceConditions,
                evaluation.Outcome,
                evaluation.CreatedAtUtc,
                slicesJson = evaluation.SlicesJson,
                reviews = evaluation.Reviews.Select(review => new
                {
                    review.EmployeeId,
                    review.AnnotationsJson,
                    review.Recommendation,
                    review.Outcome,
                    review.EvaluatedAtUtc
                })
            }),
            consents = consents.Select(consent => new
            {
                consent.ConsentType,
                consent.Version,
                consent.Language,
                consent.Source,
                consent.AcceptedAtUtc,
                consent.RevokedAtUtc,
                consent.Notes
            }),
            emailLogs = emailLogs.Select(log => new
            {
                log.CreatedAtUtc,
                log.ToAddress,
                log.Subject,
                log.Kind,
                log.Status,
                log.Error
            })
        };

        var files = new List<GdprExportFile>();
        foreach (var document in documents)
        {
            var bytes = await _documentService.GetDocumentDataAsync(document.Id, patientId);
            if (bytes != null)
            {
                files.Add(new GdprExportFile
                {
                    FileName = $"documents/{document.Id}-{SafeFileName(document.OriginalFileName)}",
                    Data = bytes
                });
            }
        }

        var photo = await _photoService.GetPatientPhotoAsync(patientId);
        if (photo != null)
        {
            files.Add(new GdprExportFile { FileName = "photo.jpg", Data = photo });
        }

        _context.GdprRequests.Add(new GdprRequest
        {
            PatientId = patientId,
            RequestType = "Export",
            Status = "Completed",
            RequestedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
            Details = $"JSON + {files.Count} file(s)"
        });
        await _context.SaveChangesAsync(cancellationToken);

        return new GdprExportBundle { Export = export, Files = files };
    }

    public async Task<(bool Ok, string? Error, object? Result)> AnonymizeAsync(
        int patientId,
        string confirmation,
        int? requestedByEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var patient = await _context.Patients
            .Include(p => p.Person)
                .ThenInclude(person => person.Address)
            .Include(p => p.Person)
                .ThenInclude(person => person.Employee)
            .Include(p => p.Person)
                .ThenInclude(person => person.ContactToObjects)
                    .ThenInclude(link => link.Contact)
                        .ThenInclude(contact => contact.Emails)
            .Include(p => p.Person)
                .ThenInclude(person => person.ContactToObjects)
                    .ThenInclude(link => link.Contact)
                        .ThenInclude(contact => contact.PhoneNumbers)
            .Include(p => p.Person)
                .ThenInclude(person => person.Comment)
            .Include(p => p.Comment)
            .FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);

        if (patient == null)
        {
            return (false, "Patient not found", null);
        }
        if (patient.Person.Employee != null)
        {
            return (false, "Zaměstnance nelze anonymizovat jako pacienta.", null);
        }

        var expected = patient.Person.UID;
        if (!string.Equals(confirmation.Trim(), expected, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(confirmation.Trim(), "ANONYMIZE", StringComparison.Ordinal))
        {
            return (false, "Potvrzení nesouhlasí s UID pacienta.", null);
        }

        var photoDeleted = await _photoService.DeletePatientPhotoAsync(patientId);
        var documentsDeleted = await _documentService.PurgeDocumentsForPatientAsync(patientId);

        // Contact data is personal data: remove phone/e-mail rows and links.
        var contactLinks = patient.Person.ContactToObjects.ToList();
        var contactIds = contactLinks.Select(link => link.ContactId).Distinct().ToList();
        if (contactIds.Count > 0)
        {
            _context.ContactEmails.RemoveRange(
                _context.ContactEmails.Where(email => contactIds.Contains(email.ContactId)));
            _context.ContactPhoneNumbers.RemoveRange(
                _context.ContactPhoneNumbers.Where(phone => contactIds.Contains(phone.ContactId)));
            _context.ContactToObjects.RemoveRange(contactLinks);
        }

        if (patient.Person.Address != null)
        {
            patient.Person.Address.Street = string.Empty;
            patient.Person.Address.City = string.Empty;
            patient.Person.Address.PostalCode = string.Empty;
            patient.Person.Address.Country = string.Empty;
        }

        patient.Person.FirstName = "Anonymizovaný";
        patient.Person.LastName = $"Pacient {patientId}";
        patient.Person.TitleBefore = null;
        patient.Person.TitleAfter = null;
        patient.Person.UID = $"ANON-{patientId}-{Guid.NewGuid():N}";
        patient.Person.Gender = "Unknown";
        patient.Person.Active = false;
        if (patient.Person.Comment != null)
        {
            patient.Person.Comment.Text = null;
        }

        patient.BirthDate = new DateTime(1900, 1, 1);
        patient.InsuranceNumber = 0;
        patient.Alive = false;
        patient.PhotoPath = null;
        patient.IsDeleted = true;
        patient.DeletedAtUtc = DateTime.UtcNow;
        patient.DeletedBy = "GDPR";
        if (patient.Comment != null)
        {
            patient.Comment.Text = null;
        }

        var visits = await _context.Visits
            .Where(visit => visit.PatientId == patientId)
            .ToListAsync(cancellationToken);
        foreach (var visit in visits)
        {
            visit.Note = null;
        }

        var events = await _context.Events
            .Include(e => e.Comment)
            .Where(e => e.PatientId == patientId)
            .ToListAsync(cancellationToken);
        foreach (var eventEntry in events)
        {
            if (eventEntry.Comment != null)
            {
                eventEntry.Comment.Text = null;
            }
        }

        var submissions = await _context.FormSubmissions
            .Include(fs => fs.Consent)
            .Include(fs => fs.Lifestyle)
            .Where(fs => fs.PatientId == patientId)
            .ToListAsync(cancellationToken);
        foreach (var submission in submissions)
        {
            if (submission.Consent != null)
            {
                submission.Consent.SignatureVector = null;
                submission.Consent.SignaturePlace = null;
            }
            if (submission.Lifestyle != null)
            {
                submission.Lifestyle.AdditionalHealthInfo = null;
            }
        }

        var consents = await _context.PatientConsents
            .Where(consent => consent.PatientId == patientId)
            .ToListAsync(cancellationToken);
        foreach (var consent in consents)
        {
            consent.RevokedAtUtc ??= DateTime.UtcNow;
        }

        var emailLogs = await _context.EmailLogs
            .Where(log => log.PersonId == patient.PersonId)
            .ToListAsync(cancellationToken);
        foreach (var log in emailLogs)
        {
            log.ToAddress = AnonymizedEmail;
            log.RecipientName = null;
            log.Error = null;
        }

        var links = await _context.IntakeFormLinks
            .Where(link => link.PersonId == patient.PersonId && link.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var link in links)
        {
            link.RevokedAtUtc = DateTime.UtcNow;
        }

        var examinationDocumentsDeleted = await _examinationDocumentService
            .PurgeForPatientAsync(patientId, cancellationToken);

        var summary = new
        {
            patientId,
            photoDeleted,
            documentsDeleted,
            examinationDocumentsDeleted,
            visitsAnonymized = visits.Count,
            eventsAnonymized = events.Count,
            formSubmissionsAnonymized = submissions.Count,
            consentsRevoked = consents.Count,
            emailLogsAnonymized = emailLogs.Count,
            intakeLinksRevoked = links.Count
        };

        _context.GdprRequests.Add(new GdprRequest
        {
            PatientId = patientId,
            RequestType = "Anonymize",
            Status = "Completed",
            RequestedByEmployeeId = requestedByEmployeeId,
            RequestedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
            Details = $"photo={photoDeleted}, documents={documentsDeleted}, examinationDocuments={examinationDocumentsDeleted}, visits={visits.Count}, events={events.Count}"
        });

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogWarning(
            "Patient {PatientId} was anonymized by employee {EmployeeId}", patientId, requestedByEmployeeId);

        return (true, null, summary);
    }

    private static string SafeFileName(string value)
    {
        // Path.GetFileName only strips '/' on Linux; backslashes are valid
        // separators on Windows extractors, so neutralise them too.
        var name = Path.GetFileName(value.Replace('\\', '/'));
        return string.IsNullOrWhiteSpace(name) ? "file" : name;
    }
}
