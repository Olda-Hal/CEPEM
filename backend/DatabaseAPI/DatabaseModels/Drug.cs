using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Drug
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<DrugUse> DrugUses { get; set; } = new List<DrugUse>();
    public ICollection<DrugToDrugCategory> DrugToDrugCategories { get; set; } = new List<DrugToDrugCategory>();
}
