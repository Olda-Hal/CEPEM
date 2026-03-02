namespace DatabaseAPI.DatabaseModels;

public class DrugCategory
{
    public int Id { get; set; }

    public int? NameTranslationId { get; set; }
    public Translation? NameTranslation { get; set; }

    // Navigation properties
    public ICollection<DrugToDrugCategory> DrugToDrugCategories { get; set; } = new List<DrugToDrugCategory>();
}
