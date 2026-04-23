using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.APIModels;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DatabaseAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly DatabaseContext _context;

    public EventsController(DatabaseContext context)
    {
        _context = context;
    }

    [HttpGet("options")]
    public async Task<ActionResult<EventOptionsResponse>> GetEventOptions([FromQuery] string? language = "cs")
    {
        var eventTypes = await _context.EventTypes.Include(et => et.NameTranslation).ToListAsync();
        var drugs = await _context.Drugs.Include(d => d.NameTranslation).ToListAsync();
        var drugCategories = await _context.DrugCategories.Include(dc => dc.NameTranslation).ToListAsync();
        var examinationTypes = await _context.ExaminationTypes.Include(et => et.NameTranslation).ToListAsync();
        var symptoms = await _context.Symptoms.Include(s => s.NameTranslation).ToListAsync();
        var injuryTypes = await _context.InjuryTypes.Include(it => it.NameTranslation).ToListAsync();
        var vaccineTypes = await _context.VaccineTypes.Include(vt => vt.NameTranslation).ToListAsync();

        string Resolve(Translation? t) => language switch
        {
            "cs" => t?.CS ?? t?.EN ?? string.Empty,
            "nl" => t?.NL ?? t?.EN ?? string.Empty,
            _    => t?.EN ?? string.Empty,
        };

        var response = new EventOptionsResponse
        {
            EventTypes = eventTypes.Select(et => new EventTypeResponse
            {
                Id = et.Id,
                Name = Resolve(et.NameTranslation)
            }).OrderBy(x => x.Name).ToList(),
            Drugs = drugs.Select(d => new DrugResponse
            {
                Id = d.Id,
                Name = Resolve(d.NameTranslation)
            }).OrderBy(x => x.Name).ToList(),
            DrugCategories = drugCategories.Select(dc => new DrugCategoryResponse
            {
                Id = dc.Id,
                Name = Resolve(dc.NameTranslation)
            }).OrderBy(x => x.Name).ToList(),
            ExaminationTypes = examinationTypes.Select(et => new ExaminationTypeResponse
            {
                Id = et.Id,
                Name = Resolve(et.NameTranslation)
            }).OrderBy(x => x.Name).ToList(),
            Symptoms = symptoms.Select(s => new SymptomResponse
            {
                Id = s.Id,
                Name = Resolve(s.NameTranslation)
            }).OrderBy(x => x.Name).ToList(),
            InjuryTypes = injuryTypes.Select(it => new InjuryTypeResponse
            {
                Id = it.Id,
                Name = Resolve(it.NameTranslation)
            }).OrderBy(x => x.Name).ToList(),
            VaccineTypes = vaccineTypes.Select(vt => new VaccineTypeResponse
            {
                Id = vt.Id,
                Name = Resolve(vt.NameTranslation)
            }).OrderBy(x => x.Name).ToList()
        };

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<CreateEventResponse>> CreateEvent(CreateEventRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            Comment? comment = null;
            if (!string.IsNullOrEmpty(request.Comment))
            {
                comment = new Comment { Text = request.Comment };
                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();
            }

            var eventEntity = new Event
            {
                PatientId = request.PatientId,
                EventTypeId = request.EventTypeId,
                HappenedAt = request.HappenedAt,
                HappenedTo = request.HappenedTo,
                CommentId = comment?.Id,
                EventGroupId = request.EventGroupId
            };

            _context.Events.Add(eventEntity);
            await _context.SaveChangesAsync();

            foreach (var drugUse in request.DrugUses)
            {
                var drugUseEntity = new DrugUse
                {
                    DrugId = drugUse.DrugId,
                    EventId = eventEntity.Id
                };
                _context.DrugUses.Add(drugUseEntity);
                await _context.SaveChangesAsync();

                // Add drug categories for this drug use
                foreach (var categoryId in drugUse.CategoryIds)
                {
                    var drugToDrugCategory = new DrugToDrugCategory
                    {
                        DrugId = drugUse.DrugId,
                        CategoryId = categoryId
                    };
                    _context.DrugToDrugCategories.Add(drugToDrugCategory);
                }
            }

            var examinationIds = new List<int>();

            foreach (var examinationTypeId in request.ExaminationTypeIds)
            {
                var examination = new Examination
                {
                    ExaminationTypeId = examinationTypeId,
                    EventId = eventEntity.Id
                };
                _context.Examinations.Add(examination);
            }

            foreach (var symptomId in request.SymptomIds)
            {
                var patientSymptom = new PatientSymptom
                {
                    SymptomId = symptomId,
                    EventId = eventEntity.Id
                };
                _context.PatientSymptoms.Add(patientSymptom);
            }

            foreach (var injuryTypeId in request.InjuryTypeIds)
            {
                var injury = new Injury
                {
                    InjuryTypeId = injuryTypeId,
                    EventId = eventEntity.Id
                };
                _context.Injuries.Add(injury);
            }

            foreach (var vaccineTypeId in request.VaccineTypeIds)
            {
                var vaccine = new Vaccine
                {
                    VaccineTypeId = vaccineTypeId,
                    EventId = eventEntity.Id
                };
                _context.Vaccines.Add(vaccine);
            }

            if (request.IsPregnant == true)
            {
                var pregnancy = new Pregnancy
                {
                    EventId = eventEntity.Id,
                    Result = request.PregnancyResult ?? false
                };
                _context.Pregnancies.Add(pregnancy);
            }

            await _context.SaveChangesAsync();
            examinationIds = await _context.Examinations
                .Where(ex => ex.EventId == eventEntity.Id)
                .Select(ex => ex.Id)
                .ToListAsync();
            await transaction.CommitAsync();

            return Ok(new CreateEventResponse
            {
                EventId = eventEntity.Id,
                ExaminationIds = examinationIds
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, $"Error creating event: {ex.Message}");
        }
    }

    [HttpPost("group")]
    public async Task<ActionResult<CreateEventGroupResponse>> CreateEventGroup(CreateEventGroupRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var eventGroupId = Guid.NewGuid();
            var eventIds = new List<int>();

            foreach (var eventRequest in request.Events)
            {
                eventRequest.PatientId = request.PatientId;
                eventRequest.EventGroupId = eventGroupId;

                Comment? comment = null;
                if (!string.IsNullOrEmpty(eventRequest.Comment))
                {
                    comment = new Comment { Text = eventRequest.Comment };
                    _context.Comments.Add(comment);
                    await _context.SaveChangesAsync();
                }

                var eventEntity = new Event
                {
                    PatientId = eventRequest.PatientId,
                    EventTypeId = eventRequest.EventTypeId,
                    HappenedAt = eventRequest.HappenedAt,
                    HappenedTo = eventRequest.HappenedTo,
                    CommentId = comment?.Id,
                    EventGroupId = eventGroupId
                };

                _context.Events.Add(eventEntity);
                await _context.SaveChangesAsync();

                foreach (var drugUse in eventRequest.DrugUses)
                {
                    var drugUseEntity = new DrugUse
                    {
                        DrugId = drugUse.DrugId,
                        EventId = eventEntity.Id
                    };
                    _context.DrugUses.Add(drugUseEntity);
                    await _context.SaveChangesAsync();

                    foreach (var categoryId in drugUse.CategoryIds)
                    {
                        var drugToDrugCategory = new DrugToDrugCategory
                        {
                            DrugId = drugUse.DrugId,
                            CategoryId = categoryId
                        };
                        _context.DrugToDrugCategories.Add(drugToDrugCategory);
                    }
                }

                foreach (var examinationTypeId in eventRequest.ExaminationTypeIds)
                {
                    var examination = new Examination
                    {
                        ExaminationTypeId = examinationTypeId,
                        EventId = eventEntity.Id
                    };
                    _context.Examinations.Add(examination);
                }

                foreach (var symptomId in eventRequest.SymptomIds)
                {
                    var patientSymptom = new PatientSymptom
                    {
                        SymptomId = symptomId,
                        EventId = eventEntity.Id
                    };
                    _context.PatientSymptoms.Add(patientSymptom);
                }

                foreach (var injuryTypeId in eventRequest.InjuryTypeIds)
                {
                    var injury = new Injury
                    {
                        InjuryTypeId = injuryTypeId,
                        EventId = eventEntity.Id
                    };
                    _context.Injuries.Add(injury);
                }

                foreach (var vaccineTypeId in eventRequest.VaccineTypeIds)
                {
                    var vaccine = new Vaccine
                    {
                        VaccineTypeId = vaccineTypeId,
                        EventId = eventEntity.Id
                    };
                    _context.Vaccines.Add(vaccine);
                }

                if (eventRequest.IsPregnant == true)
                {
                    var pregnancy = new Pregnancy
                    {
                        EventId = eventEntity.Id,
                        Result = eventRequest.PregnancyResult ?? false
                    };
                    _context.Pregnancies.Add(pregnancy);
                }

                await _context.SaveChangesAsync();
                eventIds.Add(eventEntity.Id);
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
            return StatusCode(500, $"Error creating event group: {ex.Message}");
        }
    }

    [HttpPost("examination-types")]
    public async Task<ActionResult<ExaminationTypeResponse>> CreateExaminationType([FromBody] CreateOptionRequest request)
    {
        var t = new Translation { EN = request.Name };
        _context.Translations.Add(t);
        await _context.SaveChangesAsync();
        var examinationType = new ExaminationType { NameTranslationId = t.Id };
        _context.ExaminationTypes.Add(examinationType);
        await _context.SaveChangesAsync();
        return Ok(new ExaminationTypeResponse { Id = examinationType.Id, Name = t.EN });
    }

    [HttpPost("symptoms")]
    public async Task<ActionResult<SymptomResponse>> CreateSymptom([FromBody] CreateOptionRequest request)
    {
        var t = new Translation { EN = request.Name };
        _context.Translations.Add(t);
        await _context.SaveChangesAsync();
        var symptom = new Symptom { NameTranslationId = t.Id };
        _context.Symptoms.Add(symptom);
        await _context.SaveChangesAsync();
        return Ok(new SymptomResponse { Id = symptom.Id, Name = t.EN });
    }

    [HttpPost("injury-types")]
    public async Task<ActionResult<InjuryTypeResponse>> CreateInjuryType([FromBody] CreateOptionRequest request)
    {
        var t = new Translation { EN = request.Name };
        _context.Translations.Add(t);
        await _context.SaveChangesAsync();
        var injuryType = new InjuryType { NameTranslationId = t.Id };
        _context.InjuryTypes.Add(injuryType);
        await _context.SaveChangesAsync();
        return Ok(new InjuryTypeResponse { Id = injuryType.Id, Name = t.EN });
    }

    [HttpPost("vaccine-types")]
    public async Task<ActionResult<VaccineTypeResponse>> CreateVaccineType([FromBody] CreateOptionRequest request)
    {
        var t = new Translation { EN = request.Name };
        _context.Translations.Add(t);
        await _context.SaveChangesAsync();
        var vaccineType = new VaccineType { NameTranslationId = t.Id };
        _context.VaccineTypes.Add(vaccineType);
        await _context.SaveChangesAsync();
        return Ok(new VaccineTypeResponse { Id = vaccineType.Id, Name = t.EN });
    }

    [HttpPost("drugs")]
    public async Task<ActionResult<DrugResponse>> CreateDrug([FromBody] CreateOptionRequest request)
    {
        var t = new Translation { EN = request.Name };
        _context.Translations.Add(t);
        await _context.SaveChangesAsync();
        var drug = new Drug { NameTranslationId = t.Id };
        _context.Drugs.Add(drug);
        await _context.SaveChangesAsync();
        return Ok(new DrugResponse { Id = drug.Id, Name = t.EN });
    }

    [HttpPost("drug-categories")]
    public async Task<ActionResult<DrugCategoryResponse>> CreateDrugCategory([FromBody] CreateOptionRequest request)
    {
        var t = new Translation { EN = request.Name };
        _context.Translations.Add(t);
        await _context.SaveChangesAsync();
        var drugCategory = new DrugCategory { NameTranslationId = t.Id };
        _context.DrugCategories.Add(drugCategory);
        await _context.SaveChangesAsync();
        return Ok(new DrugCategoryResponse { Id = drugCategory.Id, Name = t.EN });
    }

    [HttpPost("intake-form")]
    public async Task<ActionResult<IntakeFormEventDto>> CreateIntakeFormEvent([FromBody] CreateIntakeFormEventRequest request)
    {
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

            var intakeFormEventType = await _context.EventTypes
                .Include(et => et.NameTranslation)
                .FirstOrDefaultAsync(et => et.NameTranslation != null &&
                    (et.NameTranslation.EN == "Intake Form" ||
                     et.NameTranslation.CS == "Vstupní Formulář" ||
                     et.NameTranslation.CS == "Vstupni Formular"));

            if (intakeFormEventType == null)
                return BadRequest("Intake Form event type not found. Ensure seed data is applied.");

            // Build change summary
            var changesSummary = BuildChangesSummary(patient, request);
            
            // Update patient data
            await UpdatePatientData(patient, request);

            // Build comment with medical data
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

            await SaveFormSubmissionData(request, patient, intakeFormEvent.Id);

            return Ok(new IntakeFormEventDto
            {
                EventId = intakeFormEvent.Id,
                Weight = request.Weight,
                Height = request.Height,
                Comment = commentText,
                CreatedAt = intakeFormEvent.HappenedAt,
                PatientName = $"{patient.Person.FirstName} {patient.Person.LastName}"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating intake form event: {ex.Message}");
        }
    }

    [HttpPost("intake-form-links")]
    public async Task<ActionResult<IntakeFormLinkDto>> CreateIntakeFormLink([FromBody] CreateIntakeFormLinkRequest request)
    {
        try
        {
            var person = await _context.Persons
                .FirstOrDefaultAsync(p => p.Id == request.PersonId);

            if (person == null)
                return NotFound("Person not found");

            if (request.ReservationId.HasValue)
            {
                var reservation = await _context.Reservations
                    .FirstOrDefaultAsync(r => r.Id == request.ReservationId.Value);

                if (reservation == null)
                    return NotFound("Reservation not found");

                if (reservation.PersonId != request.PersonId)
                    return BadRequest("Reservation does not belong to this person");
            }

            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToHexString(tokenBytes).ToLowerInvariant();
            var tokenHash = ComputeTokenHash(token);

            var expiresInHours = request.ExpiresInHours.GetValueOrDefault(72);
            if (expiresInHours <= 0)
                expiresInHours = 72;

            var link = new IntakeFormLink
            {
                TokenHash = tokenHash,
                PersonId = person.Id,
                ReservationId = request.ReservationId,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(expiresInHours)
            };

            _context.IntakeFormLinks.Add(link);
            await _context.SaveChangesAsync();

            return Ok(new IntakeFormLinkDto
            {
                LinkId = link.Id,
                Token = token,
                IntakePath = $"/intake/{token}",
                ExpiresAtUtc = link.ExpiresAtUtc,
                PersonId = person.Id,
                PersonName = BuildPersonName(person.FirstName, person.LastName),
                ReservationId = link.ReservationId
            });
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
            var tokenHash = ComputeTokenHash(token);

            var link = await _context.IntakeFormLinks
                .Include(l => l.Person)
                .FirstOrDefaultAsync(l => l.TokenHash == tokenHash);

            if (link == null)
                return NotFound("Intake link not found");

            if (link.RevokedAtUtc.HasValue)
                return BadRequest("Intake link has been revoked");

            if (link.UsedAtUtc.HasValue)
                return BadRequest("Intake link has already been used");

            if (link.ExpiresAtUtc < DateTime.UtcNow)
                return BadRequest("Intake link has expired");

            return Ok(new IntakeFormLinkInfoDto
            {
                PersonId = link.PersonId,
                FirstName = link.Person.FirstName,
                LastName = link.Person.LastName,
                ReservationId = link.ReservationId,
                ExpiresAtUtc = link.ExpiresAtUtc
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
        try
        {
            var tokenHash = ComputeTokenHash(token);

            var link = await _context.IntakeFormLinks
                .Include(l => l.Person)
                .FirstOrDefaultAsync(l => l.TokenHash == tokenHash);

            if (link == null)
                return NotFound("Intake link not found");

            if (link.RevokedAtUtc.HasValue)
                return BadRequest("Intake link has been revoked");

            if (link.UsedAtUtc.HasValue)
                return BadRequest("Intake link has already been used");

            if (link.ExpiresAtUtc < DateTime.UtcNow)
                return BadRequest("Intake link has expired");

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.PersonId == link.PersonId);

            if (patient == null)
            {
                var birthDate = request.DateOfBirth ?? DateTime.UtcNow.Date;
                var uid = link.Person.UID;

                if (!int.TryParse(uid, out _))
                {
                    var existingUids = await _context.Persons
                        .Select(p => p.UID)
                        .Where(existingUid => !string.IsNullOrWhiteSpace(existingUid))
                        .ToListAsync();

                    var numericUids = existingUids
                        .Where(existingUid => int.TryParse(existingUid, out _))
                        .Select(int.Parse)
                        .OrderBy(existingUid => existingUid)
                        .ToList();

                    var nextUid = 1;
                    foreach (var existingUid in numericUids)
                    {
                        if (existingUid == nextUid)
                        {
                            nextUid++;
                        }
                        else if (existingUid > nextUid)
                        {
                            break;
                        }
                    }

                    uid = nextUid.ToString();
                    link.Person.UID = uid;
                }

                patient = new Patient
                {
                    PersonId = link.PersonId,
                    BirthDate = birthDate,
                    InsuranceNumber = request.InsuranceNumber ?? 0,
                    Alive = true
                };

                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();
            }

            request.PatientId = patient.Id;

            var intakeResult = await CreateIntakeFormEvent(request);
            if (intakeResult.Result is ObjectResult objectResult && objectResult.StatusCode >= 400)
            {
                return objectResult;
            }

            link.UsedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return intakeResult.Value ?? (intakeResult.Result as OkObjectResult)?.Value as IntakeFormEventDto ??
                   new IntakeFormEventDto
                   {
                       EventId = 0,
                       CreatedAt = DateTime.UtcNow,
                       PatientName = BuildPersonName(link.Person.FirstName, link.Person.LastName)
                   };
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error submitting intake form by link: {ex.Message}");
        }
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

    private async Task UpdatePatientData(Patient patient, CreateIntakeFormEventRequest request)
    {
        var hasChanges = false;

        // Update FirstName
        if (!string.IsNullOrEmpty(request.FirstName) && request.FirstName != patient.Person.FirstName)
        {
            // Close any open history record before adding a new one
            var openHistory = patient.Person.FirstNameHistories.FirstOrDefault(h => h.UsedTo == DateTime.MaxValue || h.UsedTo > DateTime.UtcNow.Date.AddDays(1));
            if (openHistory == null)
            {
                var history = new FirstNameHistory
                {
                    PersonId = patient.Person.Id,
                    FirstName = patient.Person.FirstName,
                    UsedFrom = patient.Person.CreatedAt,
                    UsedTo = DateTime.UtcNow
                };
                _context.FirstNameHistories.Add(history);
            }

            patient.Person.FirstName = request.FirstName;
            hasChanges = true;
        }

        // Update LastName
        if (!string.IsNullOrEmpty(request.LastName) && request.LastName != patient.Person.LastName)
        {
            var openHistory = patient.Person.LastNameHistories.FirstOrDefault(h => h.UsedTo == DateTime.MaxValue || h.UsedTo > DateTime.UtcNow.Date.AddDays(1));
            if (openHistory == null)
            {
                var history = new LastNameHistory
                {
                    PersonId = patient.Person.Id,
                    LastName = patient.Person.LastName,
                    UsedFrom = patient.Person.CreatedAt,
                    UsedTo = DateTime.UtcNow
                };
                _context.LastNameHistories.Add(history);
            }

            patient.Person.LastName = request.LastName;
            hasChanges = true;
        }

        // Update InsuranceNumber
        if (request.InsuranceNumber.HasValue && request.InsuranceNumber != patient.InsuranceNumber)
        {
            patient.InsuranceNumber = request.InsuranceNumber.Value;
            hasChanges = true;
        }

        if (request.DateOfBirth.HasValue && request.DateOfBirth.Value.Date != patient.BirthDate.Date)
        {
            patient.BirthDate = request.DateOfBirth.Value.Date;
            hasChanges = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Gender) && request.Gender != patient.Person.Gender)
        {
            patient.Person.Gender = request.Gender;
            hasChanges = true;
        }

        // Update Email and PhoneNumber via Contact
        var contact = patient.Person.ContactToObjects.FirstOrDefault()?.Contact;
        
        if (!string.IsNullOrEmpty(request.Email) || !string.IsNullOrEmpty(request.PhoneNumber))
        {
            if (contact == null)
            {
                contact = new Contact();
                _context.Contacts.Add(contact);
                await _context.SaveChangesAsync();

                _context.ContactToObjects.Add(new ContactToObject
                {
                    ContactId = contact.Id,
                    ObjectId = patient.Person.Id,
                    ObjectType = ContactObjectType.Person
                });
            }

            // Update Email
            if (!string.IsNullOrEmpty(request.Email))
            {
                var currentEmail = contact.Emails.FirstOrDefault();
                if (currentEmail == null)
                {
                    _context.ContactEmails.Add(new ContactEmail
                    {
                        ContactId = contact.Id,
                        Email = request.Email
                    });
                }
                else if (currentEmail.Email != request.Email)
                {
                    var openEmailHistory = patient.Person.EmailHistories.FirstOrDefault(h => h.UsedTo == DateTime.MaxValue || h.UsedTo > DateTime.UtcNow.Date.AddDays(1));
                    if (openEmailHistory == null)
                    {
                        var emailHistory = new EmailHistory
                        {
                            PersonId = patient.Person.Id,
                            Email = currentEmail.Email,
                            UsedFrom = patient.Person.CreatedAt,
                            UsedTo = DateTime.UtcNow
                        };
                        _context.EmailHistories.Add(emailHistory);
                    }

                    currentEmail.Email = request.Email;
                }
                hasChanges = true;
            }

            // Update PhoneNumber
            if (!string.IsNullOrEmpty(request.PhoneNumber))
            {
                var currentPhone = contact.PhoneNumbers.FirstOrDefault();
                if (currentPhone == null)
                {
                    _context.ContactPhoneNumbers.Add(new ContactPhoneNumber
                    {
                        ContactId = contact.Id,
                        PhoneNumber = request.PhoneNumber
                    });
                }
                else if (currentPhone.PhoneNumber != request.PhoneNumber)
                {
                    var openPhoneHistory = patient.Person.PhoneNumberHistories.FirstOrDefault(h => h.UsedTo == DateTime.MaxValue || h.UsedTo > DateTime.UtcNow.Date.AddDays(1));
                    if (openPhoneHistory == null)
                    {
                        var phoneHistory = new PhoneNumberHistory
                        {
                            PersonId = patient.Person.Id,
                            PhoneNumber = currentPhone.PhoneNumber,
                            UsedFrom = patient.Person.CreatedAt,
                            UsedTo = DateTime.UtcNow
                        };
                        _context.PhoneNumberHistories.Add(phoneHistory);
                    }

                    currentPhone.PhoneNumber = request.PhoneNumber;
                }
                hasChanges = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Address) || !string.IsNullOrWhiteSpace(request.PostalCode))
        {
            var (street, city) = ParseAddress(request.Address);

            if (patient.Person.Address == null)
            {
                patient.Person.Address = new Address
                {
                    Street = street,
                    City = city,
                    PostalCode = request.PostalCode ?? string.Empty,
                    Country = "CZ"
                };
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(street))
                    patient.Person.Address.Street = street;

                if (!string.IsNullOrWhiteSpace(city))
                    patient.Person.Address.City = city;

                if (!string.IsNullOrWhiteSpace(request.PostalCode))
                    patient.Person.Address.PostalCode = request.PostalCode;

                if (string.IsNullOrWhiteSpace(patient.Person.Address.Country))
                    patient.Person.Address.Country = "CZ";
            }

            hasChanges = true;
        }

        if (hasChanges)
        {
            await _context.SaveChangesAsync();
        }
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

    private static (string street, string city) ParseAddress(string? rawAddress)
    {
        if (string.IsNullOrWhiteSpace(rawAddress))
            return (string.Empty, string.Empty);

        var segments = rawAddress.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return (string.Empty, string.Empty);

        if (segments.Length == 1)
            return (segments[0], string.Empty);

        return (segments[0], segments[1]);
    }

    private static string ComputeTokenHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildPersonName(string firstName, string lastName)
    {
        return $"{firstName} {lastName}".Trim();
    }
}
