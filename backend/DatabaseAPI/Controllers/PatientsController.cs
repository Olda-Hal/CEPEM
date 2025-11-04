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
                        p.Person.Email.ToLower().Contains(search));
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
                        PhoneNumber = p.Person.PhoneNumber,
                        Email = p.Person.Email,
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
                    Email = request.Email ?? string.Empty,
                    PhoneNumber = request.PhoneNumber ?? string.Empty,
                    Gender = request.Gender,
                    UID = request.Uid,
                    TitleBefore = request.TitleBefore,
                    TitleAfter = request.TitleAfter,
                    CreatedAt = DateTime.UtcNow,
                    Active = true
                };

                _context.Persons.Add(person);
                await _context.SaveChangesAsync();

                // Then create the Patient
                var patient = new Patient
                {
                    PersonId = person.Id,
                    BirthDate = request.BirthDate,
                    InsuranceNumber = request.InsuranceNumber,
                    Alive = true
                };

                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();

                // Load the complete patient with person data
                var createdPatient = await _context.Patients
                    .Include(p => p.Person)
                    .FirstAsync(p => p.Id == patient.Id);

                var patientDto = new PatientDto
                {
                    Id = createdPatient.Id,
                    PersonId = createdPatient.PersonId,
                    FirstName = createdPatient.Person.FirstName,
                    LastName = createdPatient.Person.LastName,
                    BirthDate = createdPatient.BirthDate,
                    PhoneNumber = createdPatient.Person.PhoneNumber,
                    Email = createdPatient.Person.Email,
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
                    PhoneNumber = patient.Person.PhoneNumber,
                    Email = patient.Person.Email,
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

        [HttpGet("{id}/detail")]
        public async Task<ActionResult<PatientDetailDto>> GetPatientDetail(int id)
        {
            try
            {
                var patient = await _context.Patients
                    .Include(p => p.Person)
                        .ThenInclude(per => per.Comment)
                    .Include(p => p.Comment)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.EventType)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.Comment)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.DrugUses)
                            .ThenInclude(du => du.Drug)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.Examinations)
                            .ThenInclude(ex => ex.ExaminationType)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.PatientSymptoms)
                            .ThenInclude(ps => ps.Symptom)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.Injuries)
                            .ThenInclude(i => i.InjuryType)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.Vaccines)
                            .ThenInclude(v => v.VaccineType)
                    .Include(p => p.Events)
                        .ThenInclude(e => e.Pregnancies)
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

                var allVaccines = patient.Events.SelectMany(e => e.Vaccines.Select(v => v.VaccineType.Name)).ToList();
                var allSymptoms = patient.Events.SelectMany(e => e.PatientSymptoms.Select(ps => ps.Symptom.Name)).ToList();
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
                    PhoneNumber = patient.Person.PhoneNumber,
                    Email = patient.Person.Email,
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
                        LastVisitType = lastEvent?.EventType.Name
                    },
                    QuickPreviewSettings = new QuickPreviewSettingsDto(),
                    Events = patient.Events.OrderByDescending(e => e.HappenedAt).Select(e => new PatientEventDto
                    {
                        Id = e.Id,
                        EventTypeName = e.EventType.Name ?? "Unknown",
                        HappenedAt = e.HappenedAt,
                        HappenedTo = e.HappenedTo,
                        Comment = e.Comment?.Text,
                        DrugUses = e.DrugUses.Select(du => du.Drug.Name).ToList(),
                        Examinations = e.Examinations.Select(ex => ex.ExaminationType.Name).ToList(),
                        Symptoms = e.PatientSymptoms.Select(ps => ps.Symptom.Name).ToList(),
                        Injuries = e.Injuries.Select(i => i.InjuryType.Name).ToList(),
                        Vaccines = e.Vaccines.Select(v => v.VaccineType.Name).ToList(),
                        HasPregnancy = e.Pregnancies.Any()
                    }).ToList(),
                    Appointments = appointments.OrderByDescending(a => a.StartTime).Select(a => new PatientAppointmentDto
                    {
                        Id = a.Id,
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        DoctorName = $"{a.HospitalEmployee.Employee.Person.LastName}, {a.HospitalEmployee.Employee.Person.FirstName}",
                        EquipmentName = a.Equipment?.Name,
                        HospitalName = a.Hospital.Address ?? "Unknown Hospital"
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
    }
}
