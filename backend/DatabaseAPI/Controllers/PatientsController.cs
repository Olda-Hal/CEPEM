using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using DatabaseAPI.Data;
using DatabaseAPI.APIModels;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PatientsController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly ILogger<PatientsController> _logger;

        public PatientsController(DatabaseContext context, ILogger<PatientsController> logger)
        {
            _context = context;
            _logger = logger;
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
                        FullName = $"{p.Person.LastName}, {p.Person.FirstName}"
                    })
                    .ToListAsync();

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
                    FullName = $"{createdPatient.Person.LastName}, {createdPatient.Person.FirstName}"
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
                    FullName = $"{patient.Person.LastName}, {patient.Person.FirstName}"
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
    }
}
