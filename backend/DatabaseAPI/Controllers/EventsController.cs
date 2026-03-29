using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.APIModels;

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
                .FirstOrDefaultAsync(et => et.NameTranslation.CS == "Vstupní Formulář");

            if (intakeFormEventType == null)
                return BadRequest("Intake Form event type not found");

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
            parts.Add("COVID: Prodělal/a");
        if (request.CovidVaccine == true)
            parts.Add("Vakcinace proti COVID: Ano");
        if (request.VaccinesAfter2023 == true)
            parts.Add("Vakcinace po r. 2023: Ano");

        if (!string.IsNullOrEmpty(request.AdditionalHealthInfo))
            parts.Add($"Poznámky: {request.AdditionalHealthInfo}");

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

        if (hasChanges)
        {
            await _context.SaveChangesAsync();
        }
    }
}
