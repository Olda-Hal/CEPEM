using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class DrugToDrugCategory
{
    public int Id { get; set; }
    
    public int? CategoryId { get; set; }
    public DrugCategory? Category { get; set; }
    
    [Required]
    public int DrugId { get; set; }
    public Drug Drug { get; set; } = null!;
}
