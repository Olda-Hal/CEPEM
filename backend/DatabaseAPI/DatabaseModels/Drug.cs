namespace DatabaseAPI.DatabaseModels;

public class Drug
{
    public int Id { get; set; }

    public int? NameTranslationId { get; set; }
    public Translation? NameTranslation { get; set; }

    // Navigation properties
    public ICollection<DrugUse> DrugUses { get; set; } = new List<DrugUse>();
    public ICollection<DrugToDrugCategory> DrugToDrugCategories { get; set; } = new List<DrugToDrugCategory>();
}
