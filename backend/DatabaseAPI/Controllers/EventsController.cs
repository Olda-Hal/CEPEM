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
}
