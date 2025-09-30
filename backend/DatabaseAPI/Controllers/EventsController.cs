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
    public async Task<ActionResult<EventOptionsResponse>> GetEventOptions()
    {
        var eventTypes = await _context.EventTypes.ToListAsync();
        var drugs = await _context.Drugs.OrderBy(d => d.Name).ToListAsync();
        var examinationTypes = await _context.ExaminationTypes.OrderBy(et => et.Name).ToListAsync();
        var symptoms = await _context.Symptoms.OrderBy(s => s.Name).ToListAsync();
        var injuryTypes = await _context.InjuryTypes.OrderBy(it => it.Name).ToListAsync();
        var vaccineTypes = await _context.VaccineTypes.OrderBy(vt => vt.Name).ToListAsync();

        var response = new EventOptionsResponse
        {
            EventTypes = eventTypes.Select(et => new EventTypeResponse
            {
                Id = et.Id,
                Name = et.Name
            }).ToList(),
            Drugs = drugs.Select(d => new DrugResponse
            {
                Id = d.Id,
                Name = d.Name
            }).ToList(),
            ExaminationTypes = examinationTypes.Select(et => new ExaminationTypeResponse
            {
                Id = et.Id,
                Name = et.Name
            }).ToList(),
            Symptoms = symptoms.Select(s => new SymptomResponse
            {
                Id = s.Id,
                Name = s.Name
            }).ToList(),
            InjuryTypes = injuryTypes.Select(it => new InjuryTypeResponse
            {
                Id = it.Id,
                Name = it.Name
            }).ToList(),
            VaccineTypes = vaccineTypes.Select(vt => new VaccineTypeResponse
            {
                Id = vt.Id,
                Name = vt.Name
            }).ToList()
        };

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<int>> CreateEvent(CreateEventRequest request)
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
                CommentId = comment?.Id
            };

            _context.Events.Add(eventEntity);
            await _context.SaveChangesAsync();

            foreach (var drugId in request.DrugIds)
            {
                var drugUse = new DrugUse
                {
                    DrugId = drugId,
                    EventId = eventEntity.Id
                };
                _context.DrugUses.Add(drugUse);
            }

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
            await transaction.CommitAsync();

            return Ok(eventEntity.Id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, $"Error creating event: {ex.Message}");
        }
    }
}
