using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using DatabaseAPI.Data;
using DatabaseAPI.APIModels;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using DatabaseAPI.Utils;

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
        private readonly IActorCountryContextService _actorCountryContextService;
        private readonly IEmployeeScopeService _employeeScopeService;
        private readonly IUidService _uidService;
        private readonly IPatientService _patientService;
        private readonly IPatientMergeService _mergeService;
        private readonly ITdbAnalysisService _tdbAnalysisService;
        private readonly IAntivirusScanner _antivirusScanner;

        public PatientsController(
            DatabaseContext context,
            ILogger<PatientsController> logger,
            PatientPhotoService photoService,
            PatientDocumentService documentService,
            IActorCountryContextService actorCountryContextService,
            IEmployeeScopeService employeeScopeService,
            IUidService uidService,
            IPatientService patientService,
            IPatientMergeService mergeService,
            ITdbAnalysisService tdbAnalysisService,
            IAntivirusScanner? antivirusScanner = null)
        {
            _context = context;
            _logger = logger;
            _photoService = photoService;
            _documentService = documentService;
            _actorCountryContextService = actorCountryContextService;
            _employeeScopeService = employeeScopeService;
            _uidService = uidService;
            _patientService = patientService;
            _mergeService = mergeService;
            _tdbAnalysisService = tdbAnalysisService;
            _antivirusScanner = antivirusScanner ?? new NullAntivirusScanner();
        }

        [HttpGet("merge/preview")]
        public async Task<ActionResult<PatientMergePreviewDto>> PreviewMerge(
            [FromQuery] int sourcePatientId,
            [FromQuery] int targetPatientId)
        {
            if (!await PatientIsVisibleAsync(sourcePatientId) ||
                !await PatientIsVisibleAsync(targetPatientId))
            {
                return NotFound("Patient not found");
            }

            var preview = await _mergeService.PreviewAsync(sourcePatientId, targetPatientId);
            return preview == null
                ? NotFound("Patient not found or source/target are identical")
                : Ok(preview);
        }

        [HttpPost("merge")]
        public async Task<ActionResult<PatientMergeResultDto>> MergePatients(
            [FromBody] PatientMergeRequest request)
        {
            if (!await PatientIsVisibleAsync(request.SourcePatientId) ||
                !await PatientIsVisibleAsync(request.TargetPatientId))
            {
                return NotFound("Patient not found");
            }

            var (ok, error, result) = await _mergeService.MergeAsync(
                request.SourcePatientId,
                request.TargetPatientId,
                _actorCountryContextService.GetActorEmployeeId());

            return ok ? Ok(result) : BadRequest(error);
        }

        [HttpGet("search")]
        public async Task<ActionResult<PatientSearchResponse>> SearchPatients(
            [FromQuery] int page = 0,
            [FromQuery] int limit = 20,
            [FromQuery] string search = "",
            [FromQuery] string sortBy = "LastName",
            [FromQuery] string sortOrder = "asc",
            [FromQuery] bool includeDeleted = false,
            [FromQuery] bool? alive = null,
            [FromQuery] int? centerId = null)
        {
            try
            {
                // Hidden (soft-deleted) patients are visible only to Center Admin and above
                var actorScope = await _employeeScopeService.GetActorScopeAsync();
                if (!actorScope.CanSeeDeletedPatients)
                {
                    includeDeleted = false;
                }

                var query = _context.Patients
                .AsNoTracking()
                    .Include(p => p.Person)
                    .Where(p => includeDeleted || !p.IsDeleted)
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

                if (alive.HasValue)
                {
                    query = query.Where(p => p.Alive == alive.Value);
                }

                if (centerId.HasValue)
                {
                    query = query.Where(p => p.HomeHospitalId == centerId.Value);
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

                var rows = await query
                    .Skip(skip)
                    .Take(limit)
                    .Select(p => new
                    {
                        Dto = new PatientDto
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
                            PhotoUrl = _photoService.GetPatientPhotoUrl(p.Id, p),
                            IsDeleted = p.IsDeleted,
                            HomeHospitalId = p.HomeHospitalId,
                            HomeHospitalName = p.HomeHospital != null ? p.HomeHospital.Name : null,
                            CreatedByEmployeeId = p.CreatedByEmployeeId
                        },
                        CreatorSupervisorId = p.CreatedByEmployee != null ? p.CreatedByEmployee.CreatedByEmployeeId : null
                    })
                    .ToListAsync();

                var patients = rows
                    .Select(r =>
                    {
                        r.Dto.CanDelete = CanDeletePatient(actorScope, r.Dto.CreatedByEmployeeId, r.CreatorSupervisorId);
                        return r.Dto;
                    })
                    .ToList();

                var hasMore = totalCount > skip + limit;

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

        [HttpGet("export")]
        public async Task<IActionResult> ExportPatients(
            [FromQuery] string search = "",
            [FromQuery] bool includeDeleted = false,
            [FromQuery] bool? alive = null,
            [FromQuery] int? centerId = null,
            [FromQuery] string sortBy = "LastName",
            [FromQuery] string sortOrder = "asc")
        {
            var actorScope = await _employeeScopeService.GetActorScopeAsync();
            if (!actorScope.CanSeeDeletedPatients)
            {
                includeDeleted = false;
            }

            var query = _context.Patients
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
                .Include(p => p.HomeHospital)
                .Where(p => includeDeleted || !p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(p =>
                    p.Person.FirstName.ToLower().Contains(term) ||
                    p.Person.LastName.ToLower().Contains(term) ||
                    p.Person.UID.ToLower().Contains(term) ||
                    p.Person.ContactToObjects.Any(link =>
                        link.Contact.Emails.Any(email => email.Email.ToLower().Contains(term))));
            }
            if (alive.HasValue)
            {
                query = query.Where(p => p.Alive == alive.Value);
            }
            if (centerId.HasValue)
            {
                query = query.Where(p => p.HomeHospitalId == centerId.Value);
            }

            query = sortBy.ToLower() switch
            {
                "firstname" => sortOrder.ToLower() == "desc"
                    ? query.OrderByDescending(p => p.Person.FirstName)
                    : query.OrderBy(p => p.Person.FirstName),
                "birthdate" => sortOrder.ToLower() == "desc"
                    ? query.OrderByDescending(p => p.BirthDate)
                    : query.OrderBy(p => p.BirthDate),
                _ => sortOrder.ToLower() == "desc"
                    ? query.OrderByDescending(p => p.Person.LastName).ThenByDescending(p => p.Person.FirstName)
                    : query.OrderBy(p => p.Person.LastName).ThenBy(p => p.Person.FirstName)
            };

            var rows = await query.Take(50_000).Select(p => new
            {
                p.Person.UID,
                p.Person.FirstName,
                p.Person.LastName,
                p.BirthDate,
                p.Person.Gender,
                p.InsuranceNumber,
                Email = p.Person.ContactToObjects.SelectMany(link => link.Contact.Emails)
                    .Select(email => email.Email).FirstOrDefault(),
                Phone = p.Person.ContactToObjects.SelectMany(link => link.Contact.PhoneNumbers)
                    .Select(phone => phone.PhoneNumber).FirstOrDefault(),
                Street = p.Person.Address != null ? p.Person.Address.Street : string.Empty,
                City = p.Person.Address != null ? p.Person.Address.City : string.Empty,
                PostalCode = p.Person.Address != null ? p.Person.Address.PostalCode : string.Empty,
                Center = p.HomeHospital != null ? p.HomeHospital.Name : string.Empty,
                p.Alive,
                p.Person.CreatedAt,
                p.IsDeleted
            }).ToListAsync();

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("UID;First name;Last name;Birth date;Gender;Insurance;Email;Phone;Street;City;Postal code;Center;Alive;Created at;Deleted");
            foreach (var row in rows)
            {
                builder.AppendLine(string.Join(';',
                    CsvEscape(row.UID),
                    CsvEscape(row.FirstName),
                    CsvEscape(row.LastName),
                    row.BirthDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    CsvEscape(row.Gender),
                    row.InsuranceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    CsvEscape(row.Email ?? string.Empty),
                    CsvEscape(row.Phone ?? string.Empty),
                    CsvEscape(row.Street ?? string.Empty),
                    CsvEscape(row.City ?? string.Empty),
                    CsvEscape(row.PostalCode ?? string.Empty),
                    CsvEscape(row.Center ?? string.Empty),
                    row.Alive ? "1" : "0",
                    row.CreatedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    row.IsDeleted ? "1" : "0"));
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(builder.ToString());
            return File(bytes, "text/csv; charset=utf-8",
                $"patients-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
        }

        private static string CsvEscape(string value) =>
            value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;

        [HttpPost]
        public async Task<ActionResult<PatientDto>> CreatePatient([FromBody] CreatePatientRequest request)
        {
            try
            {
                var actorCountryCode = await _actorCountryContextService.GetActorCountryCodeAsync();

                var patient = await _patientService.CreatePatientAsync(request, actorCountryCode);

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

                var patientDto = MapToDto(patient);

                return CreatedAtAction(nameof(GetPatient), new { id = patient.Id }, patientDto);
            }
            catch (ScopeViolationException ex)
            {
                // Actor without an assigned center may not create patients
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating patient");
                return StatusCode(500, "An error occurred while creating patient");
            }
        }

        /// <summary>
        /// Soft-delete: the patient stays in the table (events, form
        /// submissions and documents must remain auditable) but is hidden
        /// from listings and detail reads unless includeDeleted is used.
        /// Only the creating employee, their direct supervisor or a
        /// (Sys)Admin may hide a patient.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePatient(int id)
        {
            try
            {
                var patient = await _context.Patients
                    .Include(p => p.CreatedByEmployee)
                    .FirstOrDefaultAsync(p => p.Id == id);
                if (patient == null || patient.IsDeleted)
                {
                    return NotFound("Patient not found");
                }

                var actorScope = await _employeeScopeService.GetActorScopeAsync();
                if (!CanDeletePatient(actorScope, patient.CreatedByEmployeeId, patient.CreatedByEmployee?.CreatedByEmployeeId))
                {
                    return StatusCode(403, "Pacienta může skrýt pouze ten, kdo jej založil, nebo jeho nadřízený.");
                }

                patient.IsDeleted = true;
                patient.DeletedAtUtc = DateTime.UtcNow;
                patient.DeletedBy = _actorCountryContextService.GetActorEmployeeId()?.ToString();
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting patient {PatientId}", id);
                throw new InvalidOperationException($"DELETE FAIL: {ex.GetType().Name}: {ex.Message}", ex);
            }
        }

        /// <summary>Undo of the soft-delete.</summary>
        [HttpPost("{id}/restore")]
        public async Task<IActionResult> RestorePatient(int id)
        {
            try
            {
                var patient = await _context.Patients
                    .Include(p => p.CreatedByEmployee)
                    .FirstOrDefaultAsync(p => p.Id == id);
                if (patient == null)
                {
                    return NotFound("Patient not found");
                }
                if (!patient.IsDeleted)
                {
                    return BadRequest("Patient is not deleted");
                }

                // Restore follows the same rule as hide: creator, their direct
                // supervisor or a full-access admin.
                var actorScope = await _employeeScopeService.GetActorScopeAsync();
                if (!CanDeletePatient(actorScope, patient.CreatedByEmployeeId, patient.CreatedByEmployee?.CreatedByEmployeeId))
                {
                    return StatusCode(403, "Pacienta může obnovit pouze ten, kdo jej založil, nebo jeho nadřízený.");
                }

                patient.IsDeleted = false;
                patient.DeletedAtUtc = null;
                patient.DeletedBy = null;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Patient restored" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring patient {PatientId}", id);
                return StatusCode(500, "An error occurred while restoring patient");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PatientDto>> GetPatient(int id)
        {
            try
            {
                var patient = await _context.Patients
                .AsNoTracking()
                    .Include(p => p.Person)
                        .ThenInclude(per => per.ContactToObjects)
                            .ThenInclude(cto => cto.Contact)
                                .ThenInclude(c => c.Emails)
                    .Include(p => p.Person)
                        .ThenInclude(per => per.ContactToObjects)
                            .ThenInclude(cto => cto.Contact)
                                .ThenInclude(c => c.PhoneNumbers)
                    .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

                if (patient == null)
                {
                    return NotFound($"Patient with id {id} not found");
                }

                var patientDto = MapToDto(patient);

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
                    .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

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

                var updatedPatientDto = MapToDto(patient);

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
                    .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

                if (patient == null)
                {
                    return NotFound($"Patient with id {id} not found");
                }

                // Load the event child collections with separate queries instead of
                // one giant Include chain: the single-query form multiplies the
                // independent child rows per event (drugUses x examinations x
                // symptoms x injuries x vaccines) and explodes on data-heavy
                // patients. EF relationship fixup attaches these to the tracked
                // event entities, so the mapping below is unchanged.
                var eventIds = patient.Events.Select(e => e.Id).ToList();

                if (eventIds.Count > 0)
                {
                    await _context.DrugUses
                        .Include(du => du.Drug)
                            .ThenInclude(d => d.NameTranslation)
                        .Where(du => eventIds.Contains(du.EventId))
                        .ToListAsync();

                    var examinations = await _context.Examinations
                        .Include(ex => ex.ExaminationType)
                            .ThenInclude(et => et.NameTranslation)
                        .Where(ex => eventIds.Contains(ex.EventId))
                        .ToListAsync();

                    var examinationIds = examinations.Select(ex => ex.Id).ToList();
                    if (examinationIds.Count > 0)
                    {
                        await _context.ExaminationDocuments
                            .Where(d => examinationIds.Contains(d.ExaminationId))
                            .ToListAsync();
                    }

                    await _context.PatientSymptoms
                        .Include(ps => ps.Symptom)
                            .ThenInclude(s => s.NameTranslation)
                        .Where(ps => eventIds.Contains(ps.EventId))
                        .ToListAsync();

                    await _context.Injuries
                        .Include(i => i.InjuryType)
                            .ThenInclude(it => it.NameTranslation)
                        .Where(i => eventIds.Contains(i.EventId))
                        .ToListAsync();

                    await _context.Vaccines
                        .Include(v => v.VaccineType)
                            .ThenInclude(vt => vt.NameTranslation)
                        .Where(v => eventIds.Contains(v.EventId))
                        .ToListAsync();

                    await _context.Pregnancies
                        .Where(pregnancy => eventIds.Contains(pregnancy.EventId))
                        .ToListAsync();
                }

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

                var reservations = await _context.Reservations
                    .Include(r => r.Doctor)
                        .ThenInclude(d => d!.Person)
                    .Include(r => r.ExaminationRoom)
                        .ThenInclude(room => room!.Hospital)
                            .ThenInclude(h => h!.Address)
                    .Include(r => r.ExaminationType)
                        .ThenInclude(et => et!.NameTranslation)
                    .Where(r => r.PersonId == patient.PersonId && r.Status != ReservationStatus.Cancelled)
                    .ToListAsync();

                var mergedAppointments = appointments.Select(a => new PatientAppointmentDto
                {
                    Id = a.Id,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    DoctorName = $"{a.HospitalEmployee.Employee.Person.LastName}, {a.HospitalEmployee.Employee.Person.FirstName}",
                    EquipmentName = a.Equipment?.Name,
                    HospitalName = a.Hospital.Address != null
                        ? $"{a.Hospital.Address.Street}, {a.Hospital.Address.City}"
                        : (a.Hospital.Name ?? "Unknown Hospital")
                }).Concat(reservations.Select(r => new PatientAppointmentDto
                {
                    Id = -r.Id,
                    StartTime = r.StartDateTime,
                    EndTime = r.EndDateTime,
                    DoctorName = !string.IsNullOrWhiteSpace(r.Doctor?.Person?.FirstName) && !string.IsNullOrWhiteSpace(r.Doctor?.Person?.LastName)
                        ? $"{r.Doctor.Person.LastName}, {r.Doctor.Person.FirstName}"
                        : "Unknown doctor",
                    EquipmentName = r.ExaminationType?.NameTranslation?.EN,
                    HospitalName = r.ExaminationRoom?.Hospital?.Address != null
                        ? $"{r.ExaminationRoom.Hospital.Address.Street}, {r.ExaminationRoom.Hospital.Address.City} · {r.ExaminationRoom.Name}"
                        : (r.ExaminationRoom?.Hospital?.Name != null
                            ? $"{r.ExaminationRoom.Hospital.Name} · {r.ExaminationRoom.Name}"
                            : (r.ExaminationRoom?.Name ?? "Unknown room"))
                })).OrderByDescending(a => a.StartTime).ToList();

                var documents = await _documentService.GetPatientDocumentsAsync(id);

                var age = DateTime.Now.Year - patient.BirthDate.Year;
                if (DateTime.Now < patient.BirthDate.AddYears(age))
                    age--;

                var allVaccines = patient.Events.SelectMany(e => e.Vaccines.Select(v => v.VaccineType.NameTranslation?.EN ?? string.Empty)).ToList();
                var allSymptoms = patient.Events.SelectMany(e => e.PatientSymptoms.Select(ps => ps.Symptom.NameTranslation?.EN ?? string.Empty)).ToList();
                var recentEvents = patient.Events.Where(e => e.HappenedAt >= DateTime.Now.AddMonths(-6)).ToList();
                var upcomingAppointments = mergedAppointments.Where(a => a.StartTime >= DateTime.Now).ToList();
                var lastEvent = patient.Events.OrderByDescending(e => e.HappenedAt).FirstOrDefault();

                var latestSubmission = patient.FormSubmissions
                    .OrderByDescending(submission => submission.SubmittedAtUtc)
                    .ThenByDescending(submission => submission.Id)
                    .FirstOrDefault();

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
                    FormSubmission = latestSubmission != null ? new FormSubmissionDto
                    {
                        Id = latestSubmission.Id,
                        PatientId = latestSubmission.PatientId,
                        EventId = latestSubmission.EventId,
                        SubmittedAtUtc = latestSubmission.SubmittedAtUtc,
                        Medication = latestSubmission.Medication != null ? new FormSubmissionMedicationDto
                        {
                            Id = latestSubmission.Medication.Id,
                            MedBloodPressure = latestSubmission.Medication.MedBloodPressure,
                            MedHeart = latestSubmission.Medication.MedHeart,
                            MedCholesterol = latestSubmission.Medication.MedCholesterol,
                            MedBloodThinners = latestSubmission.Medication.MedBloodThinners,
                            MedDiabetes = latestSubmission.Medication.MedDiabetes,
                            MedThyroid = latestSubmission.Medication.MedThyroid,
                            MedNerves = latestSubmission.Medication.MedNerves,
                            MedPsych = latestSubmission.Medication.MedPsych,
                            MedDigestion = latestSubmission.Medication.MedDigestion,
                            MedPain = latestSubmission.Medication.MedPain,
                            MedDehydration = latestSubmission.Medication.MedDehydration,
                            MedBreathing = latestSubmission.Medication.MedBreathing,
                            MedAntibiotics = latestSubmission.Medication.MedAntibiotics,
                            MedSupplements = latestSubmission.Medication.MedSupplements,
                            MedAllergies = latestSubmission.Medication.MedAllergies
                        } : null,
                        Lifestyle = latestSubmission.Lifestyle != null ? new FormSubmissionLifestyleDto
                        {
                            Id = latestSubmission.Lifestyle.Id,
                            PoorSleep = latestSubmission.Lifestyle.PoorSleep,
                            DigestiveIssues = latestSubmission.Lifestyle.DigestiveIssues,
                            PhysicalStress = latestSubmission.Lifestyle.PhysicalStress,
                            MentalStress = latestSubmission.Lifestyle.MentalStress,
                            Smoking = latestSubmission.Lifestyle.Smoking,
                            Fatigue = latestSubmission.Lifestyle.Fatigue,
                            LastMealHours = latestSubmission.Lifestyle.LastMealHours,
                            VaccinesAfter2023 = latestSubmission.Lifestyle.VaccinesAfter2023,
                            AdditionalHealthInfo = latestSubmission.Lifestyle.AdditionalHealthInfo
                        } : null,
                        ReproductiveHealth = latestSubmission.ReproductiveHealth != null ? new FormSubmissionReproductiveHealthDto
                        {
                            Id = latestSubmission.ReproductiveHealth.Id,
                            LastMenstruationDate = latestSubmission.ReproductiveHealth.LastMenstruationDate,
                            MenstruationCycleDays = latestSubmission.ReproductiveHealth.MenstruationCycleDays,
                            YearsSinceLastMenstruation = latestSubmission.ReproductiveHealth.YearsSinceLastMenstruation,
                            GaveBirth = latestSubmission.ReproductiveHealth.GaveBirth,
                            BirthCount = latestSubmission.ReproductiveHealth.BirthCount,
                            BirthWhen = latestSubmission.ReproductiveHealth.BirthWhen,
                            Breastfed = latestSubmission.ReproductiveHealth.Breastfed,
                            BreastfeedingMonths = latestSubmission.ReproductiveHealth.BreastfeedingMonths,
                            BreastfeedingInflammation = latestSubmission.ReproductiveHealth.BreastfeedingInflammation,
                            EndedWithInflammation = latestSubmission.ReproductiveHealth.EndedWithInflammation,
                            Contraception = latestSubmission.ReproductiveHealth.Contraception,
                            ContraceptionDuration = latestSubmission.ReproductiveHealth.ContraceptionDuration,
                            Estrogen = latestSubmission.ReproductiveHealth.Estrogen,
                            EstrogenType = latestSubmission.ReproductiveHealth.EstrogenType,
                            Interruption = latestSubmission.ReproductiveHealth.Interruption,
                            InterruptionCount = latestSubmission.ReproductiveHealth.InterruptionCount,
                            Miscarriage = latestSubmission.ReproductiveHealth.Miscarriage,
                            MiscarriageCount = latestSubmission.ReproductiveHealth.MiscarriageCount,
                            BreastInjury = latestSubmission.ReproductiveHealth.BreastInjury,
                            Mammogram = latestSubmission.ReproductiveHealth.Mammogram,
                            MammogramCount = latestSubmission.ReproductiveHealth.MammogramCount,
                            BreastBiopsy = latestSubmission.ReproductiveHealth.BreastBiopsy,
                            BreastImplants = latestSubmission.ReproductiveHealth.BreastImplants,
                            BreastSurgery = latestSubmission.ReproductiveHealth.BreastSurgery,
                            BreastSurgeryType = latestSubmission.ReproductiveHealth.BreastSurgeryType,
                            FamilyTumors = latestSubmission.ReproductiveHealth.FamilyTumors,
                            FamilyTumorType = latestSubmission.ReproductiveHealth.FamilyTumorType
                        } : null,
                        Consent = latestSubmission.Consent != null ? new FormSubmissionConsentDto
                        {
                            Id = latestSubmission.Consent.Id,
                            ConfirmAccuracy = latestSubmission.Consent.ConfirmAccuracy,
                            TermsAccepted = latestSubmission.Consent.TermsAccepted,
                            SignaturePlace = latestSubmission.Consent.SignaturePlace,
                            SignatureDate = latestSubmission.Consent.SignatureDate,
                            SignatureVector = latestSubmission.Consent.SignatureVector
                        } : null,
                        SicknessHistories = latestSubmission.SicknessHistories.Select(sh => new SicknessHistoryDto
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
                    Appointments = mergedAppointments,
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
                if (!await PatientIsVisibleAsync(id))
                {
                    return NotFound($"Patient with id {id} not found");
                }

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

                var photoValidationError = FileSignatureValidator.ValidatePatientPhoto(photoData);
                if (photoValidationError != null)
                {
                    return BadRequest(photoValidationError);
                }

                var photoScan = await _antivirusScanner.ScanAsync(photoData);
                if (!photoScan.Clean)
                {
                    return AntivirusFailure(photoScan);
                }

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
                if (!await PatientIsVisibleAsync(id))
                {
                    return NotFound($"Patient with id {id} not found");
                }

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
                if (!await PatientIsVisibleAsync(id))
                {
                    return NotFound($"Patient with id {id} not found");
                }

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
                if (!await PatientIsVisibleAsync(id))
                {
                    return NotFound($"Patient with id {id} not found");
                }

                if (document == null || document.Length == 0)
                {
                    return BadRequest("No document provided");
                }

                var isTdb = document.FileName.EndsWith(".tdb", StringComparison.OrdinalIgnoreCase);
                if (!isTdb && document.ContentType != "application/pdf")
                {
                    return BadRequest("File must be a PDF document or a MEIK .tdb measurement");
                }

                var maxSize = _documentService.GetMaxFileSizeBytes();
                if (document.Length > maxSize)
                {
                    return BadRequest($"Document size must not exceed {maxSize / 1024 / 1024}MB");
                }

                using var memoryStream = new MemoryStream();
                await document.CopyToAsync(memoryStream);
                var documentData = memoryStream.ToArray();

                isTdb = isTdb || FileSignatureValidator.IsTdb(documentData);
                var documentValidationError = FileSignatureValidator.ValidatePatientDocument(documentData, isTdb);
                if (documentValidationError != null)
                {
                    return BadRequest(documentValidationError);
                }

                var documentScan = await _antivirusScanner.ScanAsync(documentData);
                if (!documentScan.Clean)
                {
                    return AntivirusFailure(documentScan);
                }

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
                if (!await PatientIsVisibleAsync(id))
                {
                    return NotFound($"Patient with id {id} not found");
                }

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
        public async Task<IActionResult> GetPatientDocument(
            int patientId,
            int documentId,
            [FromQuery] bool download = false)
        {
            try
            {
                if (!await PatientIsVisibleAsync(patientId))
                {
                    return NotFound("Patient not found");
                }

                var document = await _context.PatientDocuments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == documentId &&
                                              d.PatientId == patientId &&
                                              !d.IsDeleted);
                if (document == null)
                {
                    return NotFound("Document not found");
                }

                var documentData = await _documentService.GetDocumentDataAsync(documentId, patientId);

                if (documentData == null)
                {
                    return NotFound("Document not found");
                }

                // A .tdb measurement is not a PDF. Without an explicit filename
                // and a binary content type the browser opens it in the PDF
                // viewer and shows a broken document.
                var isTdb = _tdbAnalysisService.IsTdbFileName(document.OriginalFileName) ||
                            _tdbAnalysisService.IsTdb(documentData);
                if (isTdb)
                {
                    return File(documentData, "application/octet-stream", document.OriginalFileName);
                }

                return download
                    ? File(documentData, "application/pdf", document.OriginalFileName)
                    : File(documentData, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting patient document");
                return StatusCode(500, "An error occurred while getting document");
            }
        }

        /// <summary>
        /// Parsed MEIK .tdb measurement for the in-app viewer. The raw file is
        /// never sent to the PDF viewer; the visit document list uses this
        /// endpoint to render the seven slices instead.
        /// </summary>
        [HttpGet("{patientId}/documents/{documentId}/tdb")]
        public async Task<IActionResult> GetPatientTdbMeasurement(int patientId, int documentId)
        {
            try
            {
                if (!await PatientIsVisibleAsync(patientId))
                {
                    return NotFound("Patient not found");
                }

                var document = await _context.PatientDocuments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == documentId &&
                                              d.PatientId == patientId &&
                                              !d.IsDeleted);
                if (document == null)
                {
                    return NotFound("Document not found");
                }

                var documentData = await _documentService.GetDocumentDataAsync(documentId, patientId);
                if (documentData == null)
                {
                    return NotFound("Document not found");
                }

                if (!_tdbAnalysisService.IsTdb(documentData))
                {
                    return BadRequest("Soubor není měření MEIK TOMODB v2.0 (.tdb).");
                }

                var analysis = _tdbAnalysisService.Parse(documentData);
                return Content(_tdbAnalysisService.ToJson(analysis), "application/json");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Rejected .tdb document {DocumentId}", documentId);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while parsing patient .tdb document");
                return StatusCode(500, "An error occurred while reading the measurement");
            }
        }

        [HttpDelete("{patientId}/documents/{documentId}")]
        public async Task<IActionResult> DeletePatientDocument(int patientId, int documentId)
        {
            try
            {
                if (!await PatientIsVisibleAsync(patientId))
                {
                    return NotFound("Patient not found");
                }

                var deleted = await _documentService.DeleteDocumentAsync(documentId, patientId);

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
                // Tracked on purpose: the comment entity is mutated below
                var patient = await _context.Patients
                    .Include(p => p.Person)
                    .Include(p => p.Comment)
                    .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

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

        /// <summary>
        /// Delete permission: the creating employee, their direct supervisor
        /// (the one who created their account), or a (Sys)Admin.
        /// </summary>
        /// <summary>Hidden patients behave as not found for every patient-scoped endpoint.</summary>
        private ObjectResult AntivirusFailure(AntivirusScanResult scan)
        {
            if (scan.ThreatName != null)
            {
                _logger.LogWarning("Rejected malicious upload: {Threat}", scan.ThreatName);
                return BadRequest("File failed antivirus scan.");
            }

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                scan.Error ?? "Antivirus scan is unavailable; the file was not stored.");
        }

        private Task<bool> PatientIsVisibleAsync(int id) =>
            _context.Patients.AnyAsync(p => p.Id == id && !p.IsDeleted);

        private static bool CanDeletePatient(ActorScope actor, int? creatorId, int? creatorSupervisorId)
            => actor.EmployeeId != null && (
                actor.HasFullAccess
                || actor.EmployeeId == creatorId
                || (creatorSupervisorId != null && actor.EmployeeId == creatorSupervisorId));

        private PatientDto MapToDto(Patient patient) => new PatientDto
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
            PhotoUrl = _photoService.GetPatientPhotoUrl(patient.Id, patient),
            IsDeleted = patient.IsDeleted,
            CreatedByEmployeeId = patient.CreatedByEmployeeId
        };
    }
}
