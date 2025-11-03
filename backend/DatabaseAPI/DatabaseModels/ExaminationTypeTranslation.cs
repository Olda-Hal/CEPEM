using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class ExaminationTypeTranslation
{
    public int Id { get; set; }
    
    [Required]
    public int ExaminationTypeId { get; set; }
    
    [Required]
    [MaxLength(10)]
    public string Language { get; set; } = string.Empty;
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public ExaminationType ExaminationType { get; set; } = null!;
}
