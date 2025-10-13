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
        var drugCategories = await _context.DrugCategories.OrderBy(dc => dc.Name).ToListAsync();
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
            DrugCategories = drugCategories.Select(dc => new DrugCategoryResponse
            {
                Id = dc.Id,
                Name = dc.Name ?? string.Empty
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
        var examinationType = new ExaminationType { Name = request.Name };
        _context.ExaminationTypes.Add(examinationType);
        await _context.SaveChangesAsync();

        return Ok(new ExaminationTypeResponse
        {
            Id = examinationType.Id,
            Name = examinationType.Name
        });
    }

    [HttpPost("symptoms")]
    public async Task<ActionResult<SymptomResponse>> CreateSymptom([FromBody] CreateOptionRequest request)
    {
        var symptom = new Symptom { Name = request.Name };
        _context.Symptoms.Add(symptom);
        await _context.SaveChangesAsync();

        return Ok(new SymptomResponse
        {
            Id = symptom.Id,
            Name = symptom.Name
        });
    }

    [HttpPost("injury-types")]
    public async Task<ActionResult<InjuryTypeResponse>> CreateInjuryType([FromBody] CreateOptionRequest request)
    {
        var injuryType = new InjuryType { Name = request.Name };
        _context.InjuryTypes.Add(injuryType);
        await _context.SaveChangesAsync();

        return Ok(new InjuryTypeResponse
        {
            Id = injuryType.Id,
            Name = injuryType.Name
        });
    }

    [HttpPost("vaccine-types")]
    public async Task<ActionResult<VaccineTypeResponse>> CreateVaccineType([FromBody] CreateOptionRequest request)
    {
        var vaccineType = new VaccineType { Name = request.Name };
        _context.VaccineTypes.Add(vaccineType);
        await _context.SaveChangesAsync();

        return Ok(new VaccineTypeResponse
        {
            Id = vaccineType.Id,
            Name = vaccineType.Name
        });
    }

    [HttpPost("drugs")]
    public async Task<ActionResult<DrugResponse>> CreateDrug([FromBody] CreateOptionRequest request)
    {
        var drug = new Drug { Name = request.Name };
        _context.Drugs.Add(drug);
        await _context.SaveChangesAsync();

        return Ok(new DrugResponse
        {
            Id = drug.Id,
            Name = drug.Name
        });
    }

    [HttpPost("drug-categories")]
    public async Task<ActionResult<DrugCategoryResponse>> CreateDrugCategory([FromBody] CreateOptionRequest request)
    {
        var drugCategory = new DrugCategory { Name = request.Name };
        _context.DrugCategories.Add(drugCategory);
        await _context.SaveChangesAsync();

        return Ok(new DrugCategoryResponse
        {
            Id = drugCategory.Id,
            Name = drugCategory.Name ?? string.Empty
        });
    }
}
