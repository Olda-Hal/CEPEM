using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using DatabaseAPI.Data;
using DatabaseAPI.APIModels;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;

namespace DatabaseAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PatientsController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly ILogger<PatientsController> _logger;
        private readonly PatientPhotoService _photoService;
        private readonly PatientDocumentService _documentService;

        public PatientsController(
            DatabaseContext context, 
            ILogger<PatientsController> logger,
            PatientPhotoService photoService,
            PatientDocumentService documentService)
        {
            _context = context;
            _logger = logger;
            _photoService = photoService;
            _documentService = documentService;
        }

        [HttpGet("search")]
        public async Task<ActionResult<PatientSearchResponse>> SearchPatients(
            [FromQuery] int page = 0,
            [FromQuery] int limit = 20,
            [FromQuery] string search = "",
            [FromQuery] string sortBy = "LastName",
            [FromQuery] string sortOrder = "asc")
        {
            try
            {
                var query = _context.Patients
                    .Include(p => p.Person)
                    .AsQueryable();

                // Apply search filter
                if (!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    query = query.Where(p => 
                        p.Person.FirstName.ToLower().Contains(search) ||
                        p.Person.LastName.ToLower().Contains(search) ||
                        p.Person.UID.ToLower().Contains(search) ||
                        p.Person.ContactToObjects.Any(cto =>
                            cto.Contact.Emails.Any(e => e.Email.ToLower().Contains(search))));
                }

                // Apply sorting
                switch (sortBy.ToLower())
                {
                    case "firstname":
                        query = sortOrder.ToLower() == "desc" 
                            ? query.OrderByDescending(p => p.Person.FirstName)
                            : query.OrderBy(p => p.Person.FirstName);
                        break;
                    case "lastname":
                    default:
                        query = sortOrder.ToLower() == "desc" 
                            ? query.OrderByDescending(p => p.Person.LastName).ThenByDescending(p => p.Person.FirstName)
                            : query.OrderBy(p => p.Person.LastName).ThenBy(p => p.Person.FirstName);
                        break;
                    case "birthdate":
                        query = sortOrder.ToLower() == "desc" 
                            ? query.OrderByDescending(p => p.BirthDate)
                            : query.OrderBy(p => p.BirthDate);
                        break;
                }

                var totalCount = await query.CountAsync();
                var skip = page * limit;
                
                var patients = await query
                    .Skip(skip)
                    .Take(limit)
                    .Select(p => new PatientDto
                    {
                        Id = p.Id,
                        PersonId = p.PersonId,
                        FirstName = p.Person.FirstName,
                        LastName = p.Person.LastName,
                        BirthDate = p.BirthDate,
                        PhoneNumber = p.Person.ContactToObjects.SelectMany(cto => cto.Contact.PhoneNumbers).Select(n => n.PhoneNumber).FirstOrDefault(),
                        Email = p.Person.ContactToObjects.SelectMany(cto => cto.Contact.Emails).Select(e => e.Email).FirstOrDefault(),
                        InsuranceNumber = p.InsuranceNumber,
                        Gender = p.Person.Gender,
                        CreatedAt = p.Person.CreatedAt,
                        UID = p.Person.UID,
                    TitleBefore = p.Person.TitleBefore,
                    TitleAfter = p.Person.TitleAfter,
                    Alive = p.Alive,
                    FullName = $"{p.Person.LastName}, {p.Person.FirstName}",
                    PhotoUrl = _photoService.GetPatientPhotoUrl(p.Id, p)
                })
                    .ToListAsync();                var hasMore = totalCount > skip + limit;

                var response = new PatientSearchResponse
                {
                    Patients = patients,
                    TotalCount = totalCount,
                    HasMore = hasMore
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching patients");
                return StatusCode(500, "An error occurred while searching patients");
            }
        }

[HttpPost]
        public async Task<ActionResult<PatientDto>> CreatePatient([FromBody] CreatePatientRequest request)
        {
            try
            {
                // First create the Person
                var person = new Person
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Gender = request.Gender,
                    UID = request.Uid,
                    TitleBefore = request.TitleBefore,
                    TitleAfter = request.TitleAfter,
                    CreatedAt = DateTime.UtcNow,
                    Active = true
                };

                _context.Persons.Add(person);
                await _context.SaveChangesAsync();

                // Create Contact with email and phone if provided
                if (!string.IsNullOrEmpty(request.Email) || !string.IsNullOrEmpty(request.PhoneNumber))
                {
                    var contact = new Contact();
                    _context.Contacts.Add(contact);
                    await _context.SaveChangesAsync();

                    if (!string.IsNullOrEmpty(request.Email))
                        _context.ContactEmails.Add(new ContactEmail { ContactId = contact.Id, Email = request.Email });
                    if (!string.IsNullOrEmpty(request.PhoneNumber))
                        _context.ContactPhoneNumbers.Add(new ContactPhoneNumber { ContactId = contact.Id, PhoneNumber = request.PhoneNumber });

                    _context.ContactToObjects.Add(new ContactToObject
                    {
                        ContactId = contact.Id,
                        ObjectId = person.Id,
                        ObjectType = ContactObjectType.Person
                    });
                    await _context.SaveChangesAsync();
                }

                // Then create the Patient
                var patient = new Patient
                {
                    PersonId = person.Id,
                    BirthDate = request.BirthDate,
                    InsuranceNumber = request.InsuranceNumber ?? 0,
                    Alive = true
                };

                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();

                var createdPatient = await _context.Patients
                    .Include(p => p.Person)
                        .ThenInclude(per => per.ContactToObjects)
                            .ThenInclude(cto => cto.Contact)
                                .ThenInclude(c => c.Emails)
                    .Include(p => p.Person)
                        .ThenInclude(per => per.ContactToObjects)
                            .ThenInclude(cto => cto.Contact)
                                .ThenInclude(c => c.PhoneNumbers)
                    .FirstAsync(p => p.Id == patient.Id);

                var patientDto = new PatientDto
                {
                    Id = createdPatient.Id,
                    PersonId = createdPatient.PersonId,
                    FirstName = createdPatient.Person.FirstName,
                    LastName = createdPatient.Person.LastName,
                    BirthDate = createdPatient.BirthDate,
                    PhoneNumber = createdPatient.Person.ContactToObjects.SelectMany(cto => cto.Contact.PhoneNumbers).Select(n => n.PhoneNumber).FirstOrDefault(),
                    Email = createdPatient.Person.ContactToObjects.SelectMany(cto => cto.Contact.Emails).Select(e => e.Email).FirstOrDefault(),
                    InsuranceNumber = createdPatient.InsuranceNumber,
                    Gender = createdPatient.Person.Gender,
                    CreatedAt = createdPatient.Person.CreatedAt,
                    UID = createdPatient.Person.UID,
                    TitleBefore = createdPatient.Person.TitleBefore,
                    TitleAfter = createdPatient.Person.TitleAfter,
                    Alive = createdPatient.Alive,
                    FullName = $"{createdPatient.Person.LastName}, {createdPatient.Person.FirstName}",
                    PhotoUrl = _photoService.GetPatientPhotoUrl(createdPatient.Id, createdPatient)
                };

                return CreatedAtAction(nameof(GetPatient), new { id = patient.Id }, patientDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating patient");
                return StatusCode(500, "An error occurred while creating patient");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PatientDto>> GetPatient(int id)
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
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (patient == null)
                {
                    return NotFound($"Patient with id {id} not found");
                }

                var patientDto = new PatientDto
                {
                    Id = patient.Id,
                    PersonId = patient.PersonId,
                    FirstName = patient.Person.FirstName,
                    LastName = patient.Person.LastName,
                    BirthDate = patient.BirthDate,
                    PhoneNumber = patient.Person.ContactToObjects.SelectMany(cto => cto.Contact.PhoneNumbers).Select(n => n.PhoneNumber).FirstOrDefault(),
                    Email = patient.Person.ContactToObjects.SelectMany(cto => cto.Contact.Emails).Select(e => e.Email).FirstOrDefault(),
                    InsuranceNumber = patient.InsuranceNumber,
                    Gender = patient.Person.Gender,
                    CreatedAt = patient.Person.CreatedAt,
                    UID = patient.Person.UID,
                    TitleBefore = patient.Person.TitleBefore,
                    TitleAfter = patient.Person.TitleAfter,
                    Alive = patient.Alive,
                    FullName = $"{patient.Person.LastName}, {patient.Person.FirstName}",
                    PhotoUrl = _photoService.GetPatientPhotoUrl(patient.Id, patient)
                };

                return Ok(patientDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting patient {PatientId}", id);
                return StatusCode(500, "An error occurred while getting patient");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PatientDto>> UpdatePatient(int id, [FromBody] UpdatePatientRequest request)
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
                    .Include(p => p.Comment)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (patient == null)
                {
                    return NotFound($"Patient with id {id} not found");
                }

                patient.Person.FirstName = request.FirstName;
                patient.Person.LastName = request.LastName;
                patient.Person.UID = request.Uid;
                patient.Person.Gender = request.Gender;
                patient.Person.TitleBefore = request.TitleBefore;
                patient.Person.TitleAfter = request.TitleAfter;

                patient.BirthDate = request.BirthDate;
                patient.InsuranceNumber = request.InsuranceNumber;
                patient.Alive = request.Alive;

                var personContactLink = patient.Person.ContactToObjects
                    .FirstOrDefault(cto => cto.ObjectType == ContactObjectType.Person);

                if ((request.Email ?? string.Empty).Trim().Length > 0 || (request.PhoneNumber ?? string.Empty).Trim().Length > 0)
                {
                    Contact contact;
                    if (personContactLink == null)
                    {
                        contact = new Contact();
                        _context.Contacts.Add(contact);
                        await _context.SaveChangesAsync();

                        personContactLink = new ContactToObject
                        {
                            ContactId = contact.Id,
                            ObjectId = patient.PersonId,
                            ObjectType = ContactObjectType.Person,
                            PersonId = patient.PersonId
                        };
                        _context.ContactToObjects.Add(personContactLink);
                    }
                    else
                    {
                        contact = personContactLink.Contact;
                    }

                    var trimmedEmail = request.Email?.Trim();
                    var existingEmail = contact.Emails.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(trimmedEmail))
                    {
                        if (existingEmail == null)
                        {
                            _context.ContactEmails.Add(new ContactEmail { ContactId = contact.Id, Email = trimmedEmail });
                        }
                        else
                        {
                            existingEmail.Email = trimmedEmail;
                        }
                    }
                    else
                    {
                        foreach (var email in contact.Emails.ToList())
                        {
                            _context.ContactEmails.Remove(email);
                        }
                    }

                    var trimmedPhone = request.PhoneNumber?.Trim();
                    var existingPhone = contact.PhoneNumbers.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(trimmedPhone))
                    {
                        if (existingPhone == null)
                        {
                            _context.ContactPhoneNumbers.Add(new ContactPhoneNumber { ContactId = contact.Id, PhoneNumber = trimmedPhone });
                        }
                        else
                        {
                            existingPhone.PhoneNumber = trimmedPhone;
                        }
                    }
                    else
                    {
                        foreach (var phone in contact.PhoneNumbers.ToList())
                        {
                            _context.ContactPhoneNumbers.Remove(phone);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.Comment))
                {
                    if (patient.Comment == null)
                    {
                        patient.Comment = new Comment
                        {
                            Text = request.Comment.Trim()
                        };
                        _context.Comments.Add(patient.Comment);
                    }
                    else
                    {
                        patient.Comment.Text = request.Comment.Trim();
                    }
                }
                else if (patient.Comment != null)
                {
                    patient.Comment.Text = string.Empty;
                }

                await _context.SaveChangesAsync();

                var updatedPatientDto = new PatientDto
                {
                    Id = patient.Id,
                    PersonId = patient.PersonId,
                    FirstName = patient.Person.FirstName,
                    LastName = patient.Person.LastName,
                    BirthDate = patient.BirthDate,
                    PhoneNumber = patient.Person.ContactToObjects.SelectMany(cto => cto.Contact.PhoneNumbers).Select(n => n.PhoneNumber).FirstOrDefault(),
                    Email = patient.Person.ContactToObjects.SelectMany(cto => cto.Contact.Emails).Select(e => e.Email).FirstOrDefault(),
                    InsuranceNumber = patient.InsuranceNumber,
                    Gender = patient.Person.Gender,
                    CreatedAt = patient.Person.CreatedAt,
                    UID = patient.Person.UID,
                    TitleBefore = patient.Person.TitleBefore,
                    TitleAfter = patient.Person.TitleAfter,
                    Alive = patient.Alive,
                    FullName = $"{patient.Person.LastName}, {patient.Person.FirstName}",
                    PhotoUrl = _photoService.GetPatientPhotoUrl(patient.Id, patient)
                };

                return Ok(updatedPatientDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating patient {PatientId}", id);
                return StatusCode(500, "An error occurred while updating patient");
            }
        }

        [HttpGet("{id}/detail")]
        public async Task<ActionResult<PatientDetailDto>> GetPatientDetail(int id)
        {
            try
            {
                var patient = await _context.Patients
                    .Include(p => p.Person)
                        .ThenInclude(per => per.Comment)
                    .Include(p => p.Person)
                        .ThenInclude(per => per.ContactToObjects)
                            .ThenInclude(cto => cto.Contact)
                                .ThenInclude(c => c.Emails)
                    .Include(p => p.Person)
                        .ThenInclude(per => per.ContactToObjects)
                            .ThenInclude(cto => cto.Contact)
                                .ThenInclude(c => c.PhoneNumbers)
                    .Include(p => p.Comment)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.EventType)
                            .ThenInclude(et => et.NameTranslation)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.Comment)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.DrugUses)
                            .ThenInclude(du => du.Drug)
                                .ThenInclude(d => d.NameTranslation)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.Examinations)
                            .ThenInclude(ex => ex.ExaminationType)
                                .ThenInclude(et => et.NameTranslation)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.Examinations)
                            .ThenInclude(ex => ex.Documents)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.PatientSymptoms)
                            .ThenInclude(ps => ps.Symptom)
                                .ThenInclude(s => s.NameTranslation)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.Injuries)
                            .ThenInclude(i => i.InjuryType)
                                .ThenInclude(it => it.NameTranslation)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.Vaccines)
                            .ThenInclude(v => v.VaccineType)
                                .ThenInclude(vt => vt.NameTranslation)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.Pregnancies)
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
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (patient == null)
                {
                    return NotFound($"Patient with id {id} not found");
                }

                var appointments = await _context.Appointments
                    .Include(a => a.HospitalEmployee)
                        .ThenInclude(he => he.Employee)
                            .ThenInclude(e => e.Person)
                    .Include(a => a.Equipment)
                    .Include(a => a.Hospital)
                    .Where(a => a.PersonId == patient.PersonId)
                    .ToListAsync();

                var documents = await _documentService.GetPatientDocumentsAsync(id);

                var age = DateTime.Now.Year - patient.BirthDate.Year;
                if (DateTime.Now < patient.BirthDate.AddYears(age))
                    age--;

                var allVaccines = patient.Events.SelectMany(e => e.Vaccines.Select(v => v.VaccineType.NameTranslation?.EN ?? string.Empty)).ToList();
                var allSymptoms = patient.Events.SelectMany(e => e.PatientSymptoms.Select(ps => ps.Symptom.NameTranslation?.EN ?? string.Empty)).ToList();
                var recentEvents = patient.Events.Where(e => e.HappenedAt >= DateTime.Now.AddMonths(-6)).ToList();
                var upcomingAppointments = appointments.Where(a => a.StartTime >= DateTime.Now).ToList();
                var lastEvent = patient.Events.OrderByDescending(e => e.HappenedAt).FirstOrDefault();

                var patientDetailDto = new PatientDetailDto
                {
                    Id = patient.Id,
                    PersonId = patient.PersonId,
                    FirstName = patient.Person.FirstName,
                    LastName = patient.Person.LastName,
                    BirthDate = patient.BirthDate,
                    PhoneNumber = patient.Person.ContactToObjects.SelectMany(cto => cto.Contact.PhoneNumbers).Select(n => n.PhoneNumber).FirstOrDefault(),
                    Email = patient.Person.ContactToObjects.SelectMany(cto => cto.Contact.Emails).Select(e => e.Email).FirstOrDefault(),
                    InsuranceNumber = patient.InsuranceNumber,
                    Gender = patient.Person.Gender,
                    CreatedAt = patient.Person.CreatedAt,
                    UID = patient.Person.UID,
                    TitleBefore = patient.Person.TitleBefore,
                    TitleAfter = patient.Person.TitleAfter,
                    Alive = patient.Alive,
                    FullName = $"{patient.Person.LastName}, {patient.Person.FirstName}",
                    Age = age,
                    Comment = patient.Comment?.Text ?? patient.Person.Comment?.Text,
                    PhotoUrl = _photoService.GetPatientPhotoUrl(patient.Id, patient),
                    QuickPreview = new PatientQuickPreviewDto
                    {
                        HasCovidVaccination = allVaccines.Any(v => v.ToLower().Contains("covid")),
                        HasFluVaccination = allVaccines.Any(v => v.ToLower().Contains("flu") || v.ToLower().Contains("influenza")),
                        HasDiabetes = allSymptoms.Any(s => s.ToLower().Contains("diabetes")),
                        HasHypertension = allSymptoms.Any(s => s.ToLower().Contains("hypertension") || s.ToLower().Contains("vysoký tlak")),
                        HasHeartDisease = allSymptoms.Any(s => s.ToLower().Contains("heart") || s.ToLower().Contains("srdce")),
                        HasAllergies = allSymptoms.Any(s => s.ToLower().Contains("allergy") || s.ToLower().Contains("alergie")),
                        RecentEventsCount = recentEvents.Count,
                        UpcomingAppointmentsCount = upcomingAppointments.Count,
                        LastVisit = lastEvent?.HappenedAt,
                        LastVisitType = lastEvent?.EventType.NameTranslation?.EN
                    },
                    QuickPreviewSettings = new QuickPreviewSettingsDto(),
                    FormSubmission = patient.FormSubmissions.FirstOrDefault() != null ? new FormSubmissionDto
                    {
                        Id = patient.FormSubmissions.First().Id,
                        PatientId = patient.FormSubmissions.First().PatientId,
                        EventId = patient.FormSubmissions.First().EventId,
                        SubmittedAtUtc = patient.FormSubmissions.First().SubmittedAtUtc,
                        Medication = patient.FormSubmissions.First().Medication != null ? new FormSubmissionMedicationDto
                        {
                            Id = patient.FormSubmissions.First().Medication.Id,
                            MedBloodPressure = patient.FormSubmissions.First().Medication.MedBloodPressure,
                            MedHeart = patient.FormSubmissions.First().Medication.MedHeart,
                            MedCholesterol = patient.FormSubmissions.First().Medication.MedCholesterol,
                            MedBloodThinners = patient.FormSubmissions.First().Medication.MedBloodThinners,
                            MedDiabetes = patient.FormSubmissions.First().Medication.MedDiabetes,
                            MedThyroid = patient.FormSubmissions.First().Medication.MedThyroid,
                            MedNerves = patient.FormSubmissions.First().Medication.MedNerves,
                            MedPsych = patient.FormSubmissions.First().Medication.MedPsych,
                            MedDigestion = patient.FormSubmissions.First().Medication.MedDigestion,
                            MedPain = patient.FormSubmissions.First().Medication.MedPain,
                            MedDehydration = patient.FormSubmissions.First().Medication.MedDehydration,
                            MedBreathing = patient.FormSubmissions.First().Medication.MedBreathing,
                            MedAntibiotics = patient.FormSubmissions.First().Medication.MedAntibiotics,
                            MedSupplements = patient.FormSubmissions.First().Medication.MedSupplements,
                            MedAllergies = patient.FormSubmissions.First().Medication.MedAllergies
                        } : null,
                        Lifestyle = patient.FormSubmissions.First().Lifestyle != null ? new FormSubmissionLifestyleDto
                        {
                            Id = patient.FormSubmissions.First().Lifestyle.Id,
                            PoorSleep = patient.FormSubmissions.First().Lifestyle.PoorSleep,
                            DigestiveIssues = patient.FormSubmissions.First().Lifestyle.DigestiveIssues,
                            PhysicalStress = patient.FormSubmissions.First().Lifestyle.PhysicalStress,
                            MentalStress = patient.FormSubmissions.First().Lifestyle.MentalStress,
                            Smoking = patient.FormSubmissions.First().Lifestyle.Smoking,
                            Fatigue = patient.FormSubmissions.First().Lifestyle.Fatigue,
                            LastMealHours = patient.FormSubmissions.First().Lifestyle.LastMealHours,
                            VaccinesAfter2023 = patient.FormSubmissions.First().Lifestyle.VaccinesAfter2023,
                            AdditionalHealthInfo = patient.FormSubmissions.First().Lifestyle.AdditionalHealthInfo
                        } : null,
                        ReproductiveHealth = patient.FormSubmissions.First().ReproductiveHealth != null ? new FormSubmissionReproductiveHealthDto
                        {
                            Id = patient.FormSubmissions.First().ReproductiveHealth.Id,
                            LastMenstruationDate = patient.FormSubmissions.First().ReproductiveHealth.LastMenstruationDate,
                            MenstruationCycleDays = patient.FormSubmissions.First().ReproductiveHealth.MenstruationCycleDays,
                            YearsSinceLastMenstruation = patient.FormSubmissions.First().ReproductiveHealth.YearsSinceLastMenstruation,
                            GaveBirth = patient.FormSubmissions.First().ReproductiveHealth.GaveBirth,
                            BirthCount = patient.FormSubmissions.First().ReproductiveHealth.BirthCount,
                            BirthWhen = patient.FormSubmissions.First().ReproductiveHealth.BirthWhen,
                            Breastfed = patient.FormSubmissions.First().ReproductiveHealth.Breastfed,
                            BreastfeedingMonths = patient.FormSubmissions.First().ReproductiveHealth.BreastfeedingMonths,
                            BreastfeedingInflammation = patient.FormSubmissions.First().ReproductiveHealth.BreastfeedingInflammation,
                            EndedWithInflammation = patient.FormSubmissions.First().ReproductiveHealth.EndedWithInflammation,
                            Contraception = patient.FormSubmissions.First().ReproductiveHealth.Contraception,
                            ContraceptionDuration = patient.FormSubmissions.First().ReproductiveHealth.ContraceptionDuration,
                            Estrogen = patient.FormSubmissions.First().ReproductiveHealth.Estrogen,
                            EstrogenType = patient.FormSubmissions.First().ReproductiveHealth.EstrogenType,
                            Interruption = patient.FormSubmissions.First().ReproductiveHealth.Interruption,
                            InterruptionCount = patient.FormSubmissions.First().ReproductiveHealth.InterruptionCount,
                            Miscarriage = patient.FormSubmissions.First().ReproductiveHealth.Miscarriage,
                            MiscarriageCount = patient.FormSubmissions.First().ReproductiveHealth.MiscarriageCount,
                            BreastInjury = patient.FormSubmissions.First().ReproductiveHealth.BreastInjury,
                            Mammogram = patient.FormSubmissions.First().ReproductiveHealth.Mammogram,
                            MammogramCount = patient.FormSubmissions.First().ReproductiveHealth.MammogramCount,
                            BreastBiopsy = patient.FormSubmissions.First().ReproductiveHealth.BreastBiopsy,
                            BreastImplants = patient.FormSubmissions.First().ReproductiveHealth.BreastImplants,
                            BreastSurgery = patient.FormSubmissions.First().ReproductiveHealth.BreastSurgery,
                            BreastSurgeryType = patient.FormSubmissions.First().ReproductiveHealth.BreastSurgeryType,
                            FamilyTumors = patient.FormSubmissions.First().ReproductiveHealth.FamilyTumors,
                            FamilyTumorType = patient.FormSubmissions.First().ReproductiveHealth.FamilyTumorType
                        } : null,
                        Consent = patient.FormSubmissions.First().Consent != null ? new FormSubmissionConsentDto
                        {
                            Id = patient.FormSubmissions.First().Consent.Id,
                            ConfirmAccuracy = patient.FormSubmissions.First().Consent.ConfirmAccuracy,
                            TermsAccepted = patient.FormSubmissions.First().Consent.TermsAccepted,
                            SignaturePlace = patient.FormSubmissions.First().Consent.SignaturePlace,
                            SignatureDate = patient.FormSubmissions.First().Consent.SignatureDate,
                            SignatureVector = patient.FormSubmissions.First().Consent.SignatureVector
                        } : null,
                        SicknessHistories = patient.FormSubmissions.First().SicknessHistories.Select(sh => new SicknessHistoryDto
                        {
                            Id = sh.Id,
                            SicknessName = sh.SicknessName,
                            HadSickness = sh.HadSickness,
                            SicknessWhen = sh.SicknessWhen,
                            Vaccinated = sh.Vaccinated,
                            VaccinationWhen = sh.VaccinationWhen,
                            Notes = sh.Notes
                        }).ToList()
                    } : null,
                    Events = patient.Events.OrderByDescending(e => e.HappenedAt).Select(e => new PatientEventDto
                    {
                        Id = e.Id,
                        EventTypeName = e.EventType.NameTranslation?.EN ?? "Unknown",
                        HappenedAt = e.HappenedAt,
                        HappenedTo = e.HappenedTo,
                        Comment = e.Comment?.Text,
                        DrugUses = e.DrugUses.Select(du => du.Drug.NameTranslation?.EN ?? string.Empty).ToList(),
                        Examinations = e.Examinations.Select(ex => new ExaminationWithDocumentsDto
                        {
                            Id = ex.Id,
                            Name = ex.ExaminationType.NameTranslation?.EN ?? string.Empty,
                            Documents = ex.Documents
                                .Where(d => !d.IsDeleted)
                                .Select(d => new ExaminationDocumentDto
                                {
                                    Id = d.Id,
                                    FileName = d.OriginalFileName,
                                    UploadedAt = d.UploadedAt,
                                    FileSize = d.FileSize
                                }).ToList()
                        }).ToList(),
                        Symptoms = e.PatientSymptoms.Select(ps => ps.Symptom.NameTranslation?.EN ?? string.Empty).ToList(),
                        Injuries = e.Injuries.Select(i => i.InjuryType.NameTranslation?.EN ?? string.Empty).ToList(),
                        Vaccines = e.Vaccines.Select(v => v.VaccineType.NameTranslation?.EN ?? string.Empty).ToList(),
                        HasPregnancy = e.Pregnancies.Any()
                    }).ToList(),
                    Appointments = appointments.OrderByDescending(a => a.StartTime).Select(a => new PatientAppointmentDto
                    {
                        Id = a.Id,
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        DoctorName = $"{a.HospitalEmployee.Employee.Person.LastName}, {a.HospitalEmployee.Employee.Person.FirstName}",
                        EquipmentName = a.Equipment?.Name,
                        HospitalName = a.Hospital.Address != null
                            ? $"{a.Hospital.Address.Street}, {a.Hospital.Address.City}"
                            : (a.Hospital.Name ?? "Unknown Hospital")
                    }).ToList(),
                    Documents = documents.Select(d => new PatientDocumentDto
                    {
                        Id = d.Id,
                        FileName = d.OriginalFileName,
                        UploadedAt = d.UploadedAt,
                        FileSize = d.FileSize
                    }).ToList()
                };

                return Ok(patientDetailDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting patient detail {PatientId}", id);
                return StatusCode(500, "An error occurred while getting patient detail");
            }
        }

        [HttpPost("{id}/photo")]
        public async Task<IActionResult> UploadPatientPhoto(int id, [FromForm] IFormFile photo)
        {
            try
            {
                if (photo == null || photo.Length == 0)
                {
                    return BadRequest("No photo provided");
                }

                if (!photo.ContentType.StartsWith("image/"))
                {
                    return BadRequest("File must be an image");
                }

                if (photo.Length > 5 * 1024 * 1024)
                {
                    return BadRequest("Photo size must not exceed 5MB");
                }

                using var memoryStream = new MemoryStream();
                await photo.CopyToAsync(memoryStream);
                var photoData = memoryStream.ToArray();

                var fileName = await _photoService.SavePatientPhotoAsync(id, photoData);

                return Ok(new { photoUrl = $"/api/patients/{id}/photo" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Patient not found when uploading photo");
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while uploading patient photo");
                return StatusCode(500, "An error occurred while uploading photo");
            }
        }

        [HttpGet("{id}/photo")]
        public async Task<IActionResult> GetPatientPhoto(int id)
        {
            try
            {
                var photoData = await _photoService.GetPatientPhotoAsync(id);

                if (photoData == null)
                {
                    return NotFound("Patient photo not found");
                }

                return File(photoData, "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting patient photo");
                return StatusCode(500, "An error occurred while getting photo");
            }
        }

        [HttpDelete("{id}/photo")]
        public async Task<IActionResult> DeletePatientPhoto(int id)
        {
            try
            {
                var deleted = await _photoService.DeletePatientPhotoAsync(id);

                if (!deleted)
                {
                    return NotFound("Patient photo not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting patient photo");
                return StatusCode(500, "An error occurred while deleting photo");
            }
        }

        [HttpPost("{id}/documents")]
        public async Task<IActionResult> UploadPatientDocument(int id, [FromForm] IFormFile document)
        {
            try
            {
                if (document == null || document.Length == 0)
                {
                    return BadRequest("No document provided");
                }

                if (document.ContentType != "application/pdf")
                {
                    return BadRequest("File must be a PDF document");
                }

                var maxSize = _documentService.GetMaxFileSizeBytes();
                if (document.Length > maxSize)
                {
                    return BadRequest($"Document size must not exceed {maxSize / 1024 / 1024}MB");
                }

                using var memoryStream = new MemoryStream();
                await document.CopyToAsync(memoryStream);
                var documentData = memoryStream.ToArray();

                var savedDocument = await _documentService.SavePatientDocumentAsync(id, documentData, document.FileName);

                return Ok(new 
                { 
                    id = savedDocument.Id,
                    fileName = savedDocument.OriginalFileName,
                    uploadedAt = savedDocument.UploadedAt,
                    fileSize = savedDocument.FileSize
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while uploading patient document");
                return StatusCode(500, "An error occurred while uploading document");
            }
        }

        [HttpGet("{id}/documents")]
        public async Task<ActionResult<List<PatientDocumentDto>>> GetPatientDocuments(int id)
        {
            try
            {
                var documents = await _documentService.GetPatientDocumentsAsync(id);

                var documentDtos = documents.Select(d => new PatientDocumentDto
                {
                    Id = d.Id,
                    FileName = d.OriginalFileName,
                    UploadedAt = d.UploadedAt,
                    FileSize = d.FileSize
                }).ToList();

                return Ok(documentDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting patient documents");
                return StatusCode(500, "An error occurred while getting documents");
            }
        }

        [HttpGet("{patientId}/documents/{documentId}")]
        public async Task<IActionResult> GetPatientDocument(int patientId, int documentId)
        {
            try
            {
                var documentData = await _documentService.GetDocumentDataAsync(documentId);

                if (documentData == null)
                {
                    return NotFound("Document not found");
                }

                return File(documentData, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting patient document");
                return StatusCode(500, "An error occurred while getting document");
            }
        }

        [HttpDelete("{patientId}/documents/{documentId}")]
        public async Task<IActionResult> DeletePatientDocument(int patientId, int documentId)
        {
            try
            {
                var deleted = await _documentService.DeleteDocumentAsync(documentId);

                if (!deleted)
                {
                    return NotFound("Document not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting patient document");
                return StatusCode(500, "An error occurred while deleting document");
            }
        }

        [HttpPatch("{id}/comment")]
        public async Task<IActionResult> UpdatePatientComment(int id, [FromBody] UpdateCommentRequest request)
        {
            try
            {
                var patient = await _context.Patients
                    .Include(p => p.Person)
                    .Include(p => p.Comment)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (patient == null)
                {
                    return NotFound("Patient not found");
                }

                if (patient.Comment == null)
                {
                    patient.Comment = new Comment
                    {
                        Text = request.Comment
                    };
                    _context.Comments.Add(patient.Comment);
                }
                else
                {
                    patient.Comment.Text = request.Comment;
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Comment updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating patient comment");
                return StatusCode(500, "An error occurred while updating comment");
            }
        }
    }
}
