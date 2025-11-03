using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class DrugTranslation
{
    public int Id { get; set; }
    
    [Required]
    public int DrugId { get; set; }
    
    [Required]
    [MaxLength(10)]
    public string Language { get; set; } = string.Empty;
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public Drug Drug { get; set; } = null!;
}
