using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.APIModels;
using DatabaseAPI.Services;
using System.Globalization;
using System.Text.Json;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly IActorCountryContextService _actorCountryContextService;
    private readonly IUidService _uidService;
    private readonly IEventService _eventService;
    private readonly IEventOptionsService _eventOptionsService;
    private readonly IIntakeFormLinkService _intakeFormLinkService;
    private readonly IEmployeeScopeService _employeeScopeService;
    private readonly IVisitService _visitService;
    private readonly ILogger<EventsController> _logger;
    private readonly PatientMasterDataService _patientMasterDataService;
    private readonly IPatientService _patientService;

    public EventsController(
        DatabaseContext context,
        IActorCountryContextService actorCountryContextService,
        IUidService uidService,
        IEventService eventService,
        IEventOptionsService eventOptionsService,
        IIntakeFormLinkService intakeFormLinkService,
        IEmployeeScopeService employeeScopeService,
        IVisitService visitService,
        PatientMasterDataService patientMasterDataService,
        IPatientService patientService,
        ILogger<EventsController> logger)
    {
        _context = context;
        _actorCountryContextService = actorCountryContextService;
        _uidService = uidService;
        _eventService = eventService;
        _eventOptionsService = eventOptionsService;
        _intakeFormLinkService = intakeFormLinkService;
        _employeeScopeService = employeeScopeService;
        _visitService = visitService;
        _patientMasterDataService = patientMasterDataService;
        _patientService = patientService;
        _logger = logger;
    }

    [HttpGet("options")]
    public async Task<ActionResult<EventOptionsResponse>> GetEventOptions([FromQuery] string? language = "cs")
    {
        return Ok(await _eventOptionsService.GetOptionsAsync(language ?? "cs"));
    }

    [HttpPost]
    public async Task<ActionResult<CreateEventResponse>> CreateEvent(CreateEventRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            if (!await CanAccessPatientAsync(request.PatientId))
            {
                return NotFound("Patient not found");
            }

            var actorCountryCode = await _actorCountryContextService.GetActorCountryCodeAsync();
            var (eventId, examinationIds) = await _eventService.CreateEventRecordAsync(request, request.EventGroupId, actorCountryCode);
            await transaction.CommitAsync();

            return Ok(new CreateEventResponse
            {
                EventId = eventId,
                ExaminationIds = examinationIds
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating event");
            return StatusCode(500, "Error creating event");
        }
    }

    [HttpPost("group")]
    public async Task<ActionResult<CreateEventGroupResponse>> CreateEventGroup(CreateEventGroupRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            if (!await CanAccessPatientAsync(request.PatientId))
            {
                return NotFound("Patient not found");
            }

            var actorCountryCode = await _actorCountryContextService.GetActorCountryCodeAsync();
            var eventGroupId = Guid.NewGuid();
            var eventIds = new List<int>();

            foreach (var eventRequest in request.Events)
            {
                eventRequest.PatientId = request.PatientId;
                eventRequest.EventGroupId = eventGroupId;

                var (eventId, _) = await _eventService.CreateEventRecordAsync(eventRequest, eventGroupId, actorCountryCode);
                eventIds.Add(eventId);
            }

            await transaction.CommitAsync();

            return Ok(new CreateEventGroupResponse
            {
                EventGroupId = eventGroupId,
                EventIds = eventIds
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating event group");
            return StatusCode(500, "Error creating event group");
        }
    }

    [HttpPost("examination-types")]
    public async Task<ActionResult<ExaminationTypeResponse>> CreateExaminationType([FromBody] CreateOptionRequest request)
    {
        var created = await _eventOptionsService.CreateExaminationTypeAsync(request.Name);
        return Ok(new ExaminationTypeResponse { Id = created.Id, Name = created.Name });
    }

    [HttpPost("symptoms")]
    public async Task<ActionResult<SymptomResponse>> CreateSymptom([FromBody] CreateOptionRequest request)
    {
        var created = await _eventOptionsService.CreateSymptomAsync(request.Name);
        return Ok(new SymptomResponse { Id = created.Id, Name = created.Name });
    }

    [HttpPost("injury-types")]
    public async Task<ActionResult<InjuryTypeResponse>> CreateInjuryType([FromBody] CreateOptionRequest request)
    {
        var created = await _eventOptionsService.CreateInjuryTypeAsync(request.Name);
        return Ok(new InjuryTypeResponse { Id = created.Id, Name = created.Name });
    }

    [HttpPost("vaccine-types")]
    public async Task<ActionResult<VaccineTypeResponse>> CreateVaccineType([FromBody] CreateOptionRequest request)
    {
        var created = await _eventOptionsService.CreateVaccineTypeAsync(request.Name);
        return Ok(new VaccineTypeResponse { Id = created.Id, Name = created.Name });
    }

    [HttpPost("drugs")]
    public async Task<ActionResult<DrugResponse>> CreateDrug([FromBody] CreateOptionRequest request)
    {
        var created = await _eventOptionsService.CreateDrugAsync(request.Name);
        return Ok(new DrugResponse { Id = created.Id, Name = created.Name });
    }

    [HttpPost("drug-categories")]
    public async Task<ActionResult<DrugCategoryResponse>> CreateDrugCategory([FromBody] CreateOptionRequest request)
    {
        var created = await _eventOptionsService.CreateDrugCategoryAsync(request.Name);
        return Ok(new DrugCategoryResponse { Id = created.Id, Name = created.Name });
    }

    [HttpPost("intake-form")]
    public async Task<ActionResult<IntakeFormEventDto>> CreateIntakeFormEvent(
        [FromBody] CreateIntakeFormEventRequest request)
    {
        if (!await CanAccessPatientAsync(request.PatientId))
        {
            return NotFound("Patient not found");
        }

        return await CreateIntakeFormEventCore(request, requireMasterDataApproval: false);
    }

    /// <summary>
    /// Atomically creates the patient and its intake submission. Used by the
    /// staff "add patient" flow so a failed intake never leaves a patient
    /// without a form.
    /// </summary>
    [HttpPost("intake-form/patient")]
    public async Task<ActionResult<IntakeFormEventDto>> CreatePatientWithIntake(
        [FromBody] CreatePatientWithIntakeRequest request)
    {
        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // A retried submission (e.g. the browser showed an error after the
            // first request had already committed) must not create the same
            // person again. Match on name + birth date among live patients.
            var firstName = (request.Patient.FirstName ?? string.Empty).Trim().ToLowerInvariant();
            var lastName = (request.Patient.LastName ?? string.Empty).Trim().ToLowerInvariant();
            if (firstName.Length > 0 && lastName.Length > 0)
            {
                var existingPatient = await _context.Patients
                    .AsNoTracking()
                    .Where(p => !p.IsDeleted &&
                                p.BirthDate.Date == request.Patient.BirthDate.Date &&
                                p.Person.FirstName.ToLower() == firstName &&
                                p.Person.LastName.ToLower() == lastName)
                    .Select(p => new { p.Id })
                    .FirstOrDefaultAsync();
                if (existingPatient != null)
                {
                    await transaction.RollbackAsync();
                    return Conflict(new
                    {
                        message = "Pacient se stejným jménem a datem narození už existuje. " +
                                  "Otevřete existující kartu místo zakládání duplicity.",
                        existingPatientId = existingPatient.Id
                    });
                }
            }

            var actorCountryCode = await _actorCountryContextService.GetActorCountryCodeAsync();
            var patient = await _patientService.CreatePatientAsync(request.Patient, actorCountryCode);

            var intake = request.Intake;
            intake.PatientId = patient.Id;
            intake.FirstName ??= request.Patient.FirstName;
            intake.LastName ??= request.Patient.LastName;
            intake.DateOfBirth ??= request.Patient.BirthDate;
            intake.Gender ??= request.Patient.Gender;
            intake.InsuranceNumber ??= request.Patient.InsuranceNumber;
            intake.Email ??= request.Patient.Email;
            intake.PhoneNumber ??= request.Patient.PhoneNumber;

            var result = await CreateIntakeFormEventCore(
                intake, requireMasterDataApproval: false);

            if (result.Result is ObjectResult error && error.StatusCode >= 400)
            {
                await transaction.RollbackAsync();
                return error;
            }

            var dto = result.Value
                      ?? (result.Result as OkObjectResult)?.Value as IntakeFormEventDto
                      ?? throw new InvalidOperationException("Intake form did not return a result");
            dto.PatientId = patient.Id;

            await transaction.CommitAsync();
            return Ok(dto);
        }
        catch (ScopeViolationException ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating patient with intake");
            return StatusCode(500, $"Error creating patient with intake: {ex.Message}");
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    private async Task<ActionResult<IntakeFormEventDto>> CreateIntakeFormEventCore(
        CreateIntakeFormEventRequest request,
        bool requireMasterDataApproval)
    {
        var transaction = _context.Database.CurrentTransaction == null
            ? await _context.Database.BeginTransactionAsync()
            : null;

        try
        {
            var patient = await _context.Patients
                .Include(p => p.Person)
                    .ThenInclude(per => per.Address)
                .Include(p => p.Person)
                    .ThenInclude(per => per.FirstNameHistories)
                .Include(p => p.Person)
                    .ThenInclude(per => per.LastNameHistories)
                .Include(p => p.Person)
                    .ThenInclude(per => per.EmailHistories)
                .Include(p => p.Person)
                    .ThenInclude(per => per.PhoneNumberHistories)
                .Include(p => p.Person)
                    .ThenInclude(per => per.ContactToObjects)
                        .ThenInclude(cto => cto.Contact)
                            .ThenInclude(c => c.Emails)
                .Include(p => p.Person)
                    .ThenInclude(per => per.ContactToObjects)
                        .ThenInclude(cto => cto.Contact)
                            .ThenInclude(c => c.PhoneNumbers)
                .FirstOrDefaultAsync(p => p.Id == request.PatientId);

            if (patient == null)
                return NotFound("Patient not found");

            // GDPR consent is mandatory and its wording must be published.
            // Validate before touching the patient/event data so a rejected
            // submission leaves no partial writes behind.
            var (gdprError, gdprTemplate, _) = await ResolveGdprConsentAsync(
                request, patient.Person.CountryCode);
            if (gdprError != null)
            {
                return BadRequest(gdprError);
            }

            var intakeFormEventType = await _context.EventTypes
                .Include(et => et.NameTranslation)
                .FirstOrDefaultAsync(et => et.NameTranslation != null &&
                    (et.NameTranslation.EN == "Intake Form" ||
                     et.NameTranslation.CS == "Vstupní Formulář" ||
                     et.NameTranslation.CS == "Vstupni Formular"));

            if (intakeFormEventType == null)
                return BadRequest("Intake Form event type not found. Ensure seed data is applied.");

            // Build the summary before any write; it compares the submitted
            // values with the current patient card.
            var changesSummary = BuildChangesSummary(patient, request);
            var commentText = BuildIntakeFormComment(request, changesSummary);

            Comment? comment = null;
            if (!string.IsNullOrEmpty(commentText))
            {
                comment = new Comment { Text = commentText.Trim() };
                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();
            }

            var intakeFormEvent = new Event
            {
                PatientId = request.PatientId,
                EventTypeId = intakeFormEventType.Id,
                HappenedAt = DateTime.UtcNow,
                CommentId = comment?.Id
            };

            _context.Events.Add(intakeFormEvent);
            await _context.SaveChangesAsync();

            if (requireMasterDataApproval)
            {
                // Public links must not overwrite the patient card directly.
                // Staff review the proposed master data and approve/reject it.
                if (_patientMasterDataService.HasMasterDataChanges(patient, request))
                {
                    _context.IntakeChangeRequests.Add(new IntakeChangeRequest
                    {
                        PatientId = patient.Id,
                        IntakeEventId = intakeFormEvent.Id,
                        Status = IntakeChangeStatuses.Pending,
                        ChangesJson = JsonSerializer.Serialize(request),
                        Summary = changesSummary,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }
            }
            else
            {
                // Staff-entered intake is trusted and applied immediately.
                await _patientMasterDataService.UpdatePatientDataAsync(patient, request);
            }

            await SaveFormSubmissionData(request, patient, intakeFormEvent.Id);

            // Every accepted intake form is also a recorded consent history
            // row pointing at the exact GDPR version and language the patient
            // agreed to.
            _context.PatientConsents.Add(new PatientConsent
            {
                PatientId = patient.Id,
                ConsentType = "gdpr",
                Version = gdprTemplate!.Version,
                Language = gdprTemplate.Language,
                Source = "IntakeForm",
                AcceptedAtUtc = DateTime.UtcNow,
                IntakeEventId = intakeFormEvent.Id
            });
            await _context.SaveChangesAsync();

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return Ok(new IntakeFormEventDto
            {
                EventId = intakeFormEvent.Id,
                PatientId = patient.Id,
                Weight = request.Weight,
                Height = request.Height,
                Comment = commentText,
                CreatedAt = intakeFormEvent.HappenedAt,
                PatientName = $"{patient.Person.FirstName} {patient.Person.LastName}"
            });
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
            return StatusCode(500, $"Error creating intake form event: {ex.Message}");
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    [HttpPost("intake-form-links")]
    public async Task<ActionResult<IntakeFormLinkDto>> CreateIntakeFormLink([FromBody] CreateIntakeFormLinkRequest request)
    {
        try
        {
            var expiresInHours = request.ExpiresInHours.GetValueOrDefault(72);
            var created = await _intakeFormLinkService.CreateLinkAsync(request.PersonId, request.ReservationId, expiresInHours);

            if (created == null)
                return NotFound("Person or reservation not found");

            var (link, rawToken) = created.Value;
            return Ok(new IntakeFormLinkDto
            {
                LinkId = link.Id,
                Token = rawToken,
                IntakePath = $"/intake/{rawToken}",
                ExpiresAtUtc = link.ExpiresAtUtc,
                PersonId = link.PersonId,
                PersonName = BuildPersonName(link.Person.FirstName, link.Person.LastName),
                ReservationId = link.ReservationId
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating intake form link: {ex.Message}");
        }
    }

    [HttpGet("intake-form-links/{token}")]
    public async Task<ActionResult<IntakeFormLinkInfoDto>> GetIntakeFormLinkInfo(string token)
    {
        try
        {
            var link = await _intakeFormLinkService.FindLinkAsync(token);

            if (link == null)
                return NotFound("Intake link not found");

            var invalidReason = _intakeFormLinkService.GetInvalidityReason(link);
            if (invalidReason != null)
                return BadRequest(invalidReason);

            var alreadySubmitted = link.UsedAtUtc.HasValue;
            if (!alreadySubmitted && link.VisitId.HasValue)
            {
                alreadySubmitted = await _context.Visits
                    .Where(v => v.Id == link.VisitId.Value)
                    .Select(v => v.SourceEventId != null)
                    .FirstOrDefaultAsync();
            }

            var language = MapLanguage(
                link.ReservationSlot?.CountryCode ?? link.Person.CountryCode);
            var gdprTemplate = await _context.ConsentTemplates
                .AsNoTracking()
                .Where(template => template.ConsentType == "gdpr" &&
                                   template.Language == language &&
                                   template.IsActive)
                .OrderByDescending(template => template.CreatedAtUtc)
                .ThenByDescending(template => template.Id)
                .FirstOrDefaultAsync();

            return Ok(new IntakeFormLinkInfoDto
            {
                PersonId = link.PersonId,
                FirstName = link.Person.FirstName,
                LastName = link.Person.LastName,
                ReservationId = link.ReservationId,
                ReservationSlotId = link.ReservationSlotId,
                VisitId = link.VisitId,
                ExpiresAtUtc = link.ExpiresAtUtc,
                Language = language,
                GdprAvailable = gdprTemplate != null,
                GdprVersion = gdprTemplate?.Version,
                VisitStartUtc = link.ReservationSlot?.StartDateTime ?? link.Visit?.VisitDate,
                CanEdit = link.AllowsEdits,
                AlreadySubmitted = alreadySubmitted,
                Prefill = await BuildIntakePrefillAsync(link)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error loading intake form link: {ex.Message}");
        }
    }

    [HttpPost("intake-form-links/{token}/submit")]
    public async Task<ActionResult<IntakeFormEventDto>> SubmitIntakeFormByLink(string token, [FromBody] CreateIntakeFormEventRequest request)
    {
        // Public submission (patient creation/promotion, event, form data,
        // consent, visit link and link marking) is one transaction.
        var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var link = await _intakeFormLinkService.FindLinkAsync(token);

            if (link == null)
            {
                await transaction.RollbackAsync();
                return NotFound("Intake link not found");
            }

            var invalidReason = _intakeFormLinkService.GetInvalidityReason(link);
            if (invalidReason != null)
            {
                await transaction.RollbackAsync();
                return BadRequest(invalidReason);
            }

            // Validate GDPR before creating/promoting a patient: a rejected
            // submission must not leave a half-created patient behind.
            request.GdprLanguage = MapLanguage(
                link.ReservationSlot?.CountryCode ?? link.Person.CountryCode);
            var (gdprError, gdprTemplate, _) = await ResolveGdprConsentAsync(
                request, link.Person.CountryCode);
            if (gdprError != null)
            {
                await transaction.RollbackAsync();
                return BadRequest(gdprError);
            }
            // Pin the exact version the patient saw so a version published
            // between page load and submit cannot change what gets recorded.
            request.GdprVersion = gdprTemplate!.Version;

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.PersonId == link.PersonId);

            if (patient == null)
            {
                var birthDate = request.DateOfBirth ?? DateTime.UtcNow.Date;
                var uid = link.Person.UID;

                if (!int.TryParse(uid, out _))
                {
                    uid = await _uidService.GetNextAvailableUidAsync();
                    link.Person.UID = uid;
                }

                patient = new Patient
                {
                    PersonId = link.PersonId,
                    BirthDate = birthDate,
                    InsuranceNumber = request.InsuranceNumber ?? 0,
                    Alive = true,
                    CountryCode = link.ReservationSlot?.CountryCode ?? link.Person.CountryCode,
                    HomeHospitalId = link.ReservationSlot?.HospitalId
                };

                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();
            }

            request.PatientId = patient.Id;

            var intakeResult = await CreateIntakeFormEventCore(
                request, requireMasterDataApproval: true);
            if (intakeResult.Result is ObjectResult objectResult && objectResult.StatusCode >= 400)
            {
                await transaction.RollbackAsync();
                return objectResult;
            }

            var dto = intakeResult.Value ??
                      (intakeResult.Result as OkObjectResult)?.Value as IntakeFormEventDto ??
                      new IntakeFormEventDto
                      {
                          EventId = 0,
                          CreatedAt = DateTime.UtcNow,
                          PatientName = BuildPersonName(link.Person.FirstName, link.Person.LastName)
                      };

            // Attach the (possibly edited) submission to the visit created from
            // the reservation, or to the visit the doctor opened on the tablet.
            // A linking failure must not lose the already saved submission.
            try
            {
                await _visitService.LinkIntakeEventAsync(patient.Id, link.VisitId, link.ReservationSlotId, dto.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not link intake event {EventId} to a visit", dto.EventId);
            }

            await _intakeFormLinkService.MarkUsedAsync(link);

            await transaction.CommitAsync();
            return dto;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, $"Error submitting intake form by link: {ex.Message}");
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    private async Task<bool> CanAccessPatientAsync(int patientId)
    {
        var actor = await _employeeScopeService.GetActorScopeAsync();
        return await DatabaseAPI.Utils.PatientScope
            .Apply(_context.Patients, actor)
            .AnyAsync(p => p.Id == patientId && !p.IsDeleted);
    }

    private static string MapLanguage(string? countryCode)
    {
        return countryCode?.Trim().ToUpperInvariant() switch
        {
            "NL" => "nl",
            "CZ" or null or "" => "cs",
            _ => "en"
        };
    }

    private static string NormalizeGdprLanguage(string? requested, string fallback)
    {
        var language = requested?.Trim().ToLowerInvariant();
        return language is "cs" or "en" or "nl" ? language : fallback;
    }

    /// <summary>
    /// Resolves the mandatory GDPR consent. When the caller supplies a
    /// version, only that active version is accepted (the patient agreed to
    /// exactly that wording); otherwise the newest active version is used.
    /// </summary>
    private async Task<(string? Error, ConsentTemplate? Template, string Language)> ResolveGdprConsentAsync(
        CreateIntakeFormEventRequest request, string? fallbackCountryCode)
    {
        var language = NormalizeGdprLanguage(
            request.GdprLanguage, MapLanguage(fallbackCountryCode));

        if (request.GdprAccepted != true)
        {
            return ("GDPR consent is required.", null, language);
        }

        var version = request.GdprVersion?.Trim();
        var query = _context.ConsentTemplates
            .Where(template => template.ConsentType == "gdpr" &&
                               template.Language == language &&
                               template.IsActive);
        if (!string.IsNullOrWhiteSpace(version))
        {
            query = query.Where(template => template.Version == version);
        }

        var resolved = await query
            .OrderByDescending(template => template.CreatedAtUtc)
            .ThenByDescending(template => template.Id)
            .FirstOrDefaultAsync();

        if (resolved == null)
        {
            var detail = string.IsNullOrWhiteSpace(version)
                ? $"language '{language}'"
                : $"language '{language}' and version '{version}'";
            return ($"GDPR consent text is not available for {detail}.", null, language);
        }

        return (null, resolved, language);
    }

    /// <summary>
    /// Prefills the public form from the patient card and the latest submitted
    /// form. Consent/signature fields are intentionally left blank — each
    /// visit needs a fresh signature.
    /// </summary>
    private async Task<CreateIntakeFormEventRequest> BuildIntakePrefillAsync(IntakeFormLink link)
    {
        var person = link.Person;
        var patient = person.Patient;

        // Contacts may be linked via PersonId (new rows) or only ObjectId
        // (historical rows), so accept both when building the prefill.
        var contactIds = await _context.ContactToObjects
            .Where(link => link.ObjectType == ContactObjectType.Person &&
                           (link.PersonId == person.Id || link.ObjectId == person.Id))
            .Select(link => link.ContactId)
            .Distinct()
            .ToListAsync();

        var prefill = new CreateIntakeFormEventRequest
        {
            FirstName = person.FirstName,
            LastName = person.LastName,
            Gender = person.Gender,
            Email = await _context.ContactEmails
                .Where(entry => contactIds.Contains(entry.ContactId))
                .Select(entry => entry.Email)
                .FirstOrDefaultAsync(),
            PhoneNumber = await _context.ContactPhoneNumbers
                .Where(entry => contactIds.Contains(entry.ContactId))
                .Select(entry => entry.PhoneNumber)
                .FirstOrDefaultAsync(),
            Address = person.Address?.Street,
            PostalCode = person.Address?.PostalCode,
            DateOfBirth = patient?.BirthDate,
            InsuranceNumber = patient is { InsuranceNumber: > 0 } ? patient.InsuranceNumber : null
        };

        if (patient == null)
        {
            return prefill;
        }

        // Public submissions no longer write master data directly. If a
        // pending change exists, show its proposed values so the patient can
        // continue editing what they submitted.
        var pendingChange = await _context.IntakeChangeRequests
            .Where(change => change.PatientId == patient.Id &&
                             change.Status == IntakeChangeStatuses.Pending)
            .OrderByDescending(change => change.CreatedAtUtc)
            .ThenByDescending(change => change.Id)
            .FirstOrDefaultAsync();

        if (pendingChange != null)
        {
            try
            {
                var proposed = JsonSerializer.Deserialize<CreateIntakeFormEventRequest>(
                    pendingChange.ChangesJson);
                if (proposed != null)
                {
                    prefill.FirstName = proposed.FirstName ?? prefill.FirstName;
                    prefill.LastName = proposed.LastName ?? prefill.LastName;
                    prefill.Gender = proposed.Gender ?? prefill.Gender;
                    prefill.Email = proposed.Email ?? prefill.Email;
                    prefill.PhoneNumber = proposed.PhoneNumber ?? prefill.PhoneNumber;
                    prefill.Address = proposed.Address ?? prefill.Address;
                    prefill.PostalCode = proposed.PostalCode ?? prefill.PostalCode;
                    prefill.DateOfBirth = proposed.DateOfBirth?.Date ?? prefill.DateOfBirth;
                    prefill.InsuranceNumber = proposed.InsuranceNumber ?? prefill.InsuranceNumber;
                }
            }
            catch (JsonException)
            {
                // Ignore a corrupt pending payload; the patient card prefill
                // is still usable.
            }
        }

        var latest = await _context.FormSubmissions
            .Where(submission => submission.PatientId == patient.Id)
            .Include(submission => submission.Medication)
            .Include(submission => submission.Lifestyle)
            .Include(submission => submission.ReproductiveHealth)
            .Include(submission => submission.SicknessHistories)
            .OrderByDescending(submission => submission.SubmittedAtUtc)
            .ThenByDescending(submission => submission.Id)
            .FirstOrDefaultAsync();

        if (latest == null)
        {
            return prefill;
        }

        if (latest.Medication is { } medication)
        {
            prefill.MedBloodPressure = medication.MedBloodPressure;
            prefill.MedHeart = medication.MedHeart;
            prefill.MedCholesterol = medication.MedCholesterol;
            prefill.MedBloodThinners = medication.MedBloodThinners;
            prefill.MedDiabetes = medication.MedDiabetes;
            prefill.MedThyroid = medication.MedThyroid;
            prefill.MedNerves = medication.MedNerves;
            prefill.MedPsych = medication.MedPsych;
            prefill.MedDigestion = medication.MedDigestion;
            prefill.MedPain = medication.MedPain;
            prefill.MedDehydration = medication.MedDehydration;
            prefill.MedBreathing = medication.MedBreathing;
            prefill.MedAntibiotics = medication.MedAntibiotics;
            prefill.MedSupplements = medication.MedSupplements;
            prefill.MedAllergies = medication.MedAllergies;
        }

        if (latest.Lifestyle is { } lifestyle)
        {
            prefill.PoorSleep = lifestyle.PoorSleep;
            prefill.DigestiveIssues = lifestyle.DigestiveIssues;
            prefill.PhysicalStress = lifestyle.PhysicalStress;
            prefill.MentalStress = lifestyle.MentalStress;
            prefill.Smoking = lifestyle.Smoking;
            prefill.Fatigue = lifestyle.Fatigue;
            prefill.LastMealHours = lifestyle.LastMealHours?
                .ToString("0.##", CultureInfo.InvariantCulture);
            prefill.HadCovid = lifestyle.HadCovid;
            prefill.CovidWhen = lifestyle.CovidWhen;
            prefill.CovidVaccine = lifestyle.CovidVaccine;
            prefill.VaccinesAfter2023 = lifestyle.VaccinesAfter2023;
            prefill.Weight = lifestyle.Weight;
            prefill.Height = lifestyle.Height;
            prefill.AdditionalHealthInfo = lifestyle.AdditionalHealthInfo;
        }

        if (latest.ReproductiveHealth is { } reproductive)
        {
            prefill.LastMenstruationDate = reproductive.LastMenstruationDate;
            prefill.MenstruationCycleDays = reproductive.MenstruationCycleDays;
            prefill.YearsSinceLastMenstruation = reproductive.YearsSinceLastMenstruation;
            prefill.GaveBirth = reproductive.GaveBirth;
            prefill.BirthCount = reproductive.BirthCount;
            prefill.BirthWhen = reproductive.BirthWhen;
            prefill.Breastfed = reproductive.Breastfed;
            prefill.BreastfeedingMonths = reproductive.BreastfeedingMonths;
            prefill.BreastfeedingInflammation = reproductive.BreastfeedingInflammation;
            prefill.EndedWithInflammation = reproductive.EndedWithInflammation;
            prefill.Contraception = reproductive.Contraception;
            prefill.ContraceptionDuration = reproductive.ContraceptionDuration;
            prefill.Estrogen = reproductive.Estrogen;
            prefill.EstrogenType = reproductive.EstrogenType;
            prefill.Interruption = reproductive.Interruption;
            prefill.InterruptionCount = reproductive.InterruptionCount;
            prefill.Miscarriage = reproductive.Miscarriage;
            prefill.MiscarriageCount = reproductive.MiscarriageCount;
            prefill.BreastInjury = reproductive.BreastInjury;
            prefill.Mammogram = reproductive.Mammogram;
            prefill.MammogramCount = reproductive.MammogramCount;
            prefill.BreastBiopsy = reproductive.BreastBiopsy;
            prefill.BreastImplants = reproductive.BreastImplants;
            prefill.BreastSurgery = reproductive.BreastSurgery;
            prefill.BreastSurgeryType = reproductive.BreastSurgeryType;
            prefill.FamilyTumors = reproductive.FamilyTumors;
            prefill.FamilyTumorType = reproductive.FamilyTumorType;
        }

        prefill.SicknessHistories = latest.SicknessHistories
            .OrderBy(sickness => sickness.Id)
            .Select(sickness => new SicknessHistoryRequestItem
            {
                SicknessName = sickness.SicknessName,
                HadSickness = sickness.HadSickness,
                SicknessWhen = sickness.SicknessWhen,
                Vaccinated = sickness.Vaccinated,
                VaccinationWhen = sickness.VaccinationWhen,
                Notes = sickness.Notes
            })
            .ToList();

        return prefill;
    }

    private string BuildChangesSummary(Patient patient, CreateIntakeFormEventRequest request)
    {
        var changes = new List<string>();

        // Compare basic fields
        var currentEmail = patient.Person.ContactToObjects
            .SelectMany(cto => cto.Contact.Emails)
            .Select(e => e.Email)
            .FirstOrDefault() ?? "";

        var currentPhone = patient.Person.ContactToObjects
            .SelectMany(cto => cto.Contact.PhoneNumbers)
            .Select(n => n.PhoneNumber)
            .FirstOrDefault() ?? "";

        if (!string.IsNullOrEmpty(request.FirstName) && request.FirstName != patient.Person.FirstName)
            changes.Add($"Jméno: {patient.Person.FirstName} → {request.FirstName}");

        if (!string.IsNullOrEmpty(request.LastName) && request.LastName != patient.Person.LastName)
            changes.Add($"Příjmení: {patient.Person.LastName} → {request.LastName}");

        if (request.InsuranceNumber.HasValue && request.InsuranceNumber != patient.InsuranceNumber)
            changes.Add($"Pojišťovna: {patient.InsuranceNumber} → {request.InsuranceNumber}");

        if (request.DateOfBirth.HasValue && request.DateOfBirth.Value.Date != patient.BirthDate.Date)
            changes.Add($"Datum narození: {patient.BirthDate:yyyy-MM-dd} → {request.DateOfBirth.Value:yyyy-MM-dd}");

        if (!string.IsNullOrWhiteSpace(request.Gender) && request.Gender != patient.Person.Gender)
            changes.Add($"Pohlaví: {patient.Person.Gender} → {request.Gender}");

        if (!string.IsNullOrEmpty(request.Email) && request.Email != currentEmail)
            changes.Add($"Email: {currentEmail} → {request.Email}");

        if (!string.IsNullOrEmpty(request.PhoneNumber) && request.PhoneNumber != currentPhone)
            changes.Add($"Telefon: {currentPhone} → {request.PhoneNumber}");

        if (request.Weight.HasValue)
            changes.Add($"Váha: {request.Weight} kg");

        if (request.Height.HasValue)
            changes.Add($"Výška: {request.Height} cm");

        return changes.Count > 0 ? "ZMĚNY:\n" + string.Join("\n", changes) : "";
    }

    private string BuildIntakeFormComment(CreateIntakeFormEventRequest request, string changesSummary)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(changesSummary))
            parts.Add(changesSummary);

        // Medical data
        if (request.MedBloodPressure == true) parts.Add("Léky: Tlak");
        if (request.MedHeart == true) parts.Add("Léky: Srdce");
        if (request.MedCholesterol == true) parts.Add("Léky: Cholesterol");
        if (request.MedBloodThinners == true) parts.Add("Léky: Srážlivost");
        if (request.MedDiabetes == true) parts.Add("Léky: Cukrovka");
        if (request.MedThyroid == true) parts.Add("Léky: Štítná žláza");
        if (request.MedNerves == true) parts.Add("Léky: Nervy");
        if (request.MedPsych == true) parts.Add("Léky: Psychika");
        if (request.MedDigestion == true) parts.Add("Léky: Zažívání");
        if (request.MedPain == true) parts.Add("Léky: Bolest");
        if (request.MedDehydration == true) parts.Add("Léky: Odvodnění");
        if (request.MedBreathing == true) parts.Add("Léky: Dýchání");
        if (request.MedAntibiotics == true) parts.Add("Léky: Antibiotika");
        if (request.MedSupplements == true) parts.Add("Léky: Doplňky stravy");
        if (request.MedAllergies == true) parts.Add("Léky: Alergie");

        if (request.PoorSleep == true) parts.Add("Zdravotní stav: Špatný spánek");
        if (request.DigestiveIssues == true) parts.Add("Zdravotní stav: Zažívání");
        if (request.PhysicalStress == true) parts.Add("Zdravotní stav: Fyzická zátěž");
        if (request.MentalStress == true) parts.Add("Zdravotní stav: Psychická zátěž");
        if (request.Smoking == true) parts.Add("Zdravotní stav: Kouření");
        if (request.Fatigue == true) parts.Add("Zdravotní stav: Pocity únavy");
        if (!string.IsNullOrEmpty(request.LastMealHours)) parts.Add($"Zdravotní stav: Poslední jídlo před {request.LastMealHours} hod");

        if (request.HadCovid == true)
            parts.Add($"COVID: Prodělal/a ({request.CovidWhen ?? "neuvedeno kdy"})");
        if (request.CovidVaccine == true)
            parts.Add("Vakcinace proti COVID: Ano");
        if (request.VaccinesAfter2023 == true)
            parts.Add("Vakcinace po r. 2023: Ano");

        if (!string.IsNullOrEmpty(request.AdditionalHealthInfo))
            parts.Add($"Poznámky: {request.AdditionalHealthInfo}");

        if (!string.IsNullOrEmpty(request.LastMenstruationDate)) parts.Add($"Poslední menzes: {request.LastMenstruationDate}");
        if (request.MenstruationCycleDays.HasValue) parts.Add($"Cyklus opakování: {request.MenstruationCycleDays} dnů");
        if (request.YearsSinceLastMenstruation.HasValue) parts.Add($"Roky od poslední menzes: {request.YearsSinceLastMenstruation}");
        if (request.GaveBirth == true) parts.Add($"Rodila: {request.BirthCount?.ToString() ?? "?"}x ({request.BirthWhen ?? "?"})");
        if (request.Breastfed == true) parts.Add($"Kojila: {request.BreastfeedingMonths?.ToString() ?? "?"} měsíců");
        if (request.BreastfeedingInflammation == true) parts.Add("Záněty při kojení: Ano");
        if (request.EndedWithInflammation == true) parts.Add("Končilo kojení zánětem: Ano");
        if (request.Contraception == true) parts.Add($"Antikoncepce: {request.ContraceptionDuration ?? "Ano"}");
        if (request.Estrogen == true) parts.Add($"Estrogen: {request.EstrogenType ?? "Ano"}");
        if (request.Interruption == true) parts.Add($"Interrupce: {request.InterruptionCount?.ToString() ?? "?"}x");
        if (request.Miscarriage == true) parts.Add($"Potrat: {request.MiscarriageCount?.ToString() ?? "?"}x");
        if (request.BreastInjury == true) parts.Add("Úraz prsu: Ano");
        if (request.Mammogram == true) parts.Add($"RTG mamograf: {request.MammogramCount?.ToString() ?? "?"}x");
        if (request.BreastBiopsy == true) parts.Add("Biopsie prsu: Ano");
        if (request.BreastImplants == true) parts.Add("Implantáty: Ano");
        if (request.BreastSurgery == true) parts.Add($"Operace prsu: {request.BreastSurgeryType ?? "Ano"}");
        if (request.FamilyTumors == true) parts.Add($"Nádory v rodině: {request.FamilyTumorType ?? "Ano"}");

        return string.Join("\n", parts);
    }

    private async Task SaveFormSubmissionData(CreateIntakeFormEventRequest request, Patient patient, int eventId)
    {
        var submission = new FormSubmission
        {
            PatientId = patient.Id,
            EventId = eventId,
            SubmittedAtUtc = DateTime.UtcNow
        };
        _context.FormSubmissions.Add(submission);
        await _context.SaveChangesAsync();

        var medication = new FormSubmissionMedication
        {
            FormSubmissionId = submission.Id,
            MedBloodPressure = request.MedBloodPressure == true,
            MedHeart = request.MedHeart == true,
            MedCholesterol = request.MedCholesterol == true,
            MedBloodThinners = request.MedBloodThinners == true,
            MedDiabetes = request.MedDiabetes == true,
            MedThyroid = request.MedThyroid == true,
            MedNerves = request.MedNerves == true,
            MedPsych = request.MedPsych == true,
            MedDigestion = request.MedDigestion == true,
            MedPain = request.MedPain == true,
            MedDehydration = request.MedDehydration == true,
            MedBreathing = request.MedBreathing == true,
            MedAntibiotics = request.MedAntibiotics == true,
            MedSupplements = request.MedSupplements == true,
            MedAllergies = request.MedAllergies == true
        };

        var lifestyle = new FormSubmissionLifestyle
        {
            FormSubmissionId = submission.Id,
            PoorSleep = request.PoorSleep == true,
            DigestiveIssues = request.DigestiveIssues == true,
            PhysicalStress = request.PhysicalStress == true,
            MentalStress = request.MentalStress == true,
            Smoking = request.Smoking == true,
            Fatigue = request.Fatigue == true,
            LastMealHours = ParseNullableFloat(request.LastMealHours),
            VaccinesAfter2023 = request.VaccinesAfter2023 == true,
            HadCovid = request.HadCovid == true,
            CovidWhen = request.CovidWhen,
            CovidVaccine = request.CovidVaccine == true,
            Weight = request.Weight,
            Height = request.Height,
            AdditionalHealthInfo = request.AdditionalHealthInfo
        };

        var reproductiveHealth = new FormSubmissionReproductiveHealth
        {
            FormSubmissionId = submission.Id,
            LastMenstruationDate = request.LastMenstruationDate,
            MenstruationCycleDays = request.MenstruationCycleDays,
            YearsSinceLastMenstruation = request.YearsSinceLastMenstruation,
            GaveBirth = request.GaveBirth == true,
            BirthCount = request.BirthCount,
            BirthWhen = request.BirthWhen,
            Breastfed = request.Breastfed == true,
            BreastfeedingMonths = request.BreastfeedingMonths,
            BreastfeedingInflammation = request.BreastfeedingInflammation == true,
            EndedWithInflammation = request.EndedWithInflammation == true,
            Contraception = request.Contraception == true,
            ContraceptionDuration = request.ContraceptionDuration,
            Estrogen = request.Estrogen == true,
            EstrogenType = request.EstrogenType,
            Interruption = request.Interruption == true,
            InterruptionCount = request.InterruptionCount,
            Miscarriage = request.Miscarriage == true,
            MiscarriageCount = request.MiscarriageCount,
            BreastInjury = request.BreastInjury == true,
            Mammogram = request.Mammogram == true,
            MammogramCount = request.MammogramCount,
            BreastBiopsy = request.BreastBiopsy == true,
            BreastImplants = request.BreastImplants == true,
            BreastSurgery = request.BreastSurgery == true,
            BreastSurgeryType = request.BreastSurgeryType,
            FamilyTumors = request.FamilyTumors == true,
            FamilyTumorType = request.FamilyTumorType
        };

        var consent = new FormSubmissionConsent
        {
            FormSubmissionId = submission.Id,
            ConfirmAccuracy = request.ConfirmAccuracy == true,
            TermsAccepted = request.TermsAccepted == true,
            SignaturePlace = string.IsNullOrWhiteSpace(request.SignaturePlace) ? null : request.SignaturePlace,
            SignatureDate = request.SignatureDate,
            SignatureVector = string.IsNullOrWhiteSpace(request.SignatureVector) ? null : request.SignatureVector
        };

        _context.FormSubmissionMedications.Add(medication);
        _context.FormSubmissionLifestyles.Add(lifestyle);
        _context.FormSubmissionReproductiveHealths.Add(reproductiveHealth);
        _context.FormSubmissionConsents.Add(consent);

        foreach (var sickness in NormalizeSicknessHistory(request))
        {
            sickness.FormSubmissionId = submission.Id;
            _context.SicknessHistories.Add(sickness);
        }

        await _context.SaveChangesAsync();
    }

    private IEnumerable<SicknessHistory> NormalizeSicknessHistory(CreateIntakeFormEventRequest request)
    {
        var sicknesses = new List<SicknessHistory>();

        if (request.SicknessHistories is { Count: > 0 })
        {
            sicknesses.AddRange(request.SicknessHistories
                .Where(s =>
                    !string.IsNullOrWhiteSpace(s.SicknessName) &&
                    (s.HadSickness == true ||
                     !string.IsNullOrWhiteSpace(s.SicknessWhen) ||
                     s.Vaccinated == true ||
                     !string.IsNullOrWhiteSpace(s.VaccinationWhen) ||
                     !string.IsNullOrWhiteSpace(s.Notes)))
                .Select(s => new SicknessHistory
                {
                    SicknessName = s.SicknessName!.Trim(),
                    HadSickness = s.HadSickness,
                    SicknessWhen = s.SicknessWhen,
                    Vaccinated = s.Vaccinated,
                    VaccinationWhen = s.VaccinationWhen,
                    Notes = s.Notes
                }));
        }

        if (!sicknesses.Any() && (request.HadCovid.HasValue || !string.IsNullOrWhiteSpace(request.CovidWhen) || request.CovidVaccine.HasValue))
        {
            sicknesses.Add(new SicknessHistory
            {
                SicknessName = "COVID-19",
                HadSickness = request.HadCovid,
                SicknessWhen = request.CovidWhen,
                Vaccinated = request.CovidVaccine,
                Notes = request.VaccinesAfter2023 == true ? "Other vaccines after 2023: yes" : null
            });
        }

        return sicknesses;
    }

    private static float? ParseNullableFloat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantParsed))
            return invariantParsed;

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var localParsed))
            return localParsed;

        return null;
    }

    private static string BuildPersonName(string firstName, string lastName)
    {
        return $"{firstName} {lastName}".Trim();
    }
}
