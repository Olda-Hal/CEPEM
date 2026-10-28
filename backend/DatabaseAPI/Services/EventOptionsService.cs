using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

/// <summary>
/// The static option sets (event/examination/drug/symptom/injury/vaccine
/// types) used by the event creation UI, including ad-hoc option creation.
/// </summary>
public interface IEventOptionsService
{
    Task<EventOptionsResponse> GetOptionsAsync(string language);

    Task<(int Id, string Name)> CreateExaminationTypeAsync(string name);
    Task<(int Id, string Name)> CreateSymptomAsync(string name);
    Task<(int Id, string Name)> CreateInjuryTypeAsync(string name);
    Task<(int Id, string Name)> CreateVaccineTypeAsync(string name);
    Task<(int Id, string Name)> CreateDrugAsync(string name);
    Task<(int Id, string Name)> CreateDrugCategoryAsync(string name);
}

public class EventOptionsService : IEventOptionsService
{
    private readonly DatabaseContext _context;

    public EventOptionsService(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<EventOptionsResponse> GetOptionsAsync(string language)
    {
        string Resolve(Translation? t) => language switch
        {
            "cs" => t?.CS ?? t?.EN ?? string.Empty,
            "nl" => t?.NL ?? t?.EN ?? string.Empty,
            _ => t?.EN ?? string.Empty,
        };

        var eventTypes = await _context.EventTypes.Include(et => et.NameTranslation).ToListAsync();
        var drugs = await _context.Drugs.Include(d => d.NameTranslation).ToListAsync();
        var drugCategories = await _context.DrugCategories.Include(dc => dc.NameTranslation).ToListAsync();
        var examinationTypes = await _context.ExaminationTypes.Include(et => et.NameTranslation).ToListAsync();
        var symptoms = await _context.Symptoms.Include(s => s.NameTranslation).ToListAsync();
        var injuryTypes = await _context.InjuryTypes.Include(it => it.NameTranslation).ToListAsync();
        var vaccineTypes = await _context.VaccineTypes.Include(vt => vt.NameTranslation).ToListAsync();

        return new EventOptionsResponse
        {
            EventTypes = eventTypes.Select(et => new EventTypeResponse { Id = et.Id, Name = Resolve(et.NameTranslation) })
                .OrderBy(x => x.Name).ToList(),
            Drugs = drugs.Select(d => new DrugResponse { Id = d.Id, Name = Resolve(d.NameTranslation) })
                .OrderBy(x => x.Name).ToList(),
            DrugCategories = drugCategories.Select(dc => new DrugCategoryResponse { Id = dc.Id, Name = Resolve(dc.NameTranslation) })
                .OrderBy(x => x.Name).ToList(),
            ExaminationTypes = examinationTypes.Select(et => new ExaminationTypeResponse { Id = et.Id, Name = Resolve(et.NameTranslation) })
                .OrderBy(x => x.Name).ToList(),
            Symptoms = symptoms.Select(s => new SymptomResponse { Id = s.Id, Name = Resolve(s.NameTranslation) })
                .OrderBy(x => x.Name).ToList(),
            InjuryTypes = injuryTypes.Select(it => new InjuryTypeResponse { Id = it.Id, Name = Resolve(it.NameTranslation) })
                .OrderBy(x => x.Name).ToList(),
            VaccineTypes = vaccineTypes.Select(vt => new VaccineTypeResponse { Id = vt.Id, Name = Resolve(vt.NameTranslation) })
                .OrderBy(x => x.Name).ToList()
        };
    }

    private async Task<(int Id, string Name)> CreateWithTranslationAsync<TEntity>(string name, Action<TEntity, int> assignTranslationId)
        where TEntity : class, new()
    {
        var translation = new Translation { EN = name };
        _context.Translations.Add(translation);
        await _context.SaveChangesAsync();

        var entity = new TEntity();
        assignTranslationId(entity, translation.Id);
        _context.Set<TEntity>().Add(entity);
        await _context.SaveChangesAsync();

        var id = (int)entity.GetType().GetProperty(nameof(EventType.Id))!.GetValue(entity)!;
        return (id, translation.EN);
    }

    public async Task<(int Id, string Name)> CreateExaminationTypeAsync(string name)
    {
        var translation = new Translation { EN = name };
        _context.Translations.Add(translation);
        await _context.SaveChangesAsync();

        var type = new ExaminationType { NameTranslationId = translation.Id };
        _context.ExaminationTypes.Add(type);
        await _context.SaveChangesAsync();
        return (type.Id, translation.EN);
    }

    public async Task<(int Id, string Name)> CreateSymptomAsync(string name)
    {
        var translation = new Translation { EN = name };
        _context.Translations.Add(translation);
        await _context.SaveChangesAsync();

        var symptom = new Symptom { NameTranslationId = translation.Id };
        _context.Symptoms.Add(symptom);
        await _context.SaveChangesAsync();
        return (symptom.Id, translation.EN);
    }

    public async Task<(int Id, string Name)> CreateInjuryTypeAsync(string name)
    {
        var translation = new Translation { EN = name };
        _context.Translations.Add(translation);
        await _context.SaveChangesAsync();

        var type = new InjuryType { NameTranslationId = translation.Id };
        _context.InjuryTypes.Add(type);
        await _context.SaveChangesAsync();
        return (type.Id, translation.EN);
    }

    public async Task<(int Id, string Name)> CreateVaccineTypeAsync(string name)
    {
        var translation = new Translation { EN = name };
        _context.Translations.Add(translation);
        await _context.SaveChangesAsync();

        var type = new VaccineType { NameTranslationId = translation.Id };
        _context.VaccineTypes.Add(type);
        await _context.SaveChangesAsync();
        return (type.Id, translation.EN);
    }

    public async Task<(int Id, string Name)> CreateDrugAsync(string name)
    {
        var translation = new Translation { EN = name };
        _context.Translations.Add(translation);
        await _context.SaveChangesAsync();

        var drug = new Drug { NameTranslationId = translation.Id };
        _context.Drugs.Add(drug);
        await _context.SaveChangesAsync();
        return (drug.Id, translation.EN);
    }

    public async Task<(int Id, string Name)> CreateDrugCategoryAsync(string name)
    {
        var translation = new Translation { EN = name };
        _context.Translations.Add(translation);
        await _context.SaveChangesAsync();

        var category = new DrugCategory { NameTranslationId = translation.Id };
        _context.DrugCategories.Add(category);
        await _context.SaveChangesAsync();
        return (category.Id, translation.EN);
    }
}
