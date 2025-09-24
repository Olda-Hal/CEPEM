using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using DatabaseAPI.Data;
using DatabaseAPI.APIModels;

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
    }
}
