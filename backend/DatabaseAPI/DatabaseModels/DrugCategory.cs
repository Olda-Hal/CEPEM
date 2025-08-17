using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class DrugCategory
{
    public int Id { get; set; }
    
    public string? Name { get; set; }
    
    // Navigation properties
    public ICollection<DrugToDrugCategory> DrugToDrugCategories { get; set; } = new List<DrugToDrugCategory>();
}
