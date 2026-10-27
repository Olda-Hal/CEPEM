using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

public interface IEventService
{
    /// <summary>
    /// Persists a single event (comment, drug uses, examinations, symptoms,
    /// injuries, vaccines, pregnancy) inside the caller's transaction and
    /// returns the new event id plus the created examination ids.
    /// </summary>
    Task<(int EventId, List<int> ExaminationIds)> CreateEventRecordAsync(
        CreateEventRequest request,
        Guid? eventGroupId,
        string actorCountryCode);
}

public class EventService : IEventService
{
    private readonly DatabaseContext _context;

    public EventService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<(int EventId, List<int> ExaminationIds)> CreateEventRecordAsync(
        CreateEventRequest request,
        Guid? eventGroupId,
        string actorCountryCode)
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
            CountryCode = actorCountryCode,
            EventGroupId = eventGroupId
        };

        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var examinations = new List<Examination>();
        foreach (var examinationTypeId in request.ExaminationTypeIds)
        {
            var examination = new Examination
            {
                ExaminationTypeId = examinationTypeId,
                EventId = eventEntity.Id,
                CountryCode = actorCountryCode
            };
            _context.Examinations.Add(examination);
            examinations.Add(examination);
        }

        foreach (var drugUse in request.DrugUses)
        {
            _context.DrugUses.Add(new DrugUse
            {
                DrugId = drugUse.DrugId,
                EventId = eventEntity.Id
            });
            await _context.SaveChangesAsync();

            foreach (var categoryId in drugUse.CategoryIds)
            {
                _context.DrugToDrugCategories.Add(new DrugToDrugCategory
                {
                    DrugId = drugUse.DrugId,
                    CategoryId = categoryId
                });
            }
        }

        foreach (var symptomId in request.SymptomIds)
        {
            _context.PatientSymptoms.Add(new PatientSymptom
            {
                SymptomId = symptomId,
                EventId = eventEntity.Id
            });
        }

        foreach (var injuryTypeId in request.InjuryTypeIds)
        {
            _context.Injuries.Add(new Injury
            {
                InjuryTypeId = injuryTypeId,
                EventId = eventEntity.Id
            });
        }

        foreach (var vaccineTypeId in request.VaccineTypeIds)
        {
            _context.Vaccines.Add(new Vaccine
            {
                VaccineTypeId = vaccineTypeId,
                EventId = eventEntity.Id
            });
        }

        if (request.IsPregnant == true)
        {
            _context.Pregnancies.Add(new Pregnancy
            {
                EventId = eventEntity.Id,
                Result = request.PregnancyResult ?? false
            });
        }

        await _context.SaveChangesAsync();
        return (eventEntity.Id, examinations.Select(examination => examination.Id).ToList());
    }
}
