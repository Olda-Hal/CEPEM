using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class ExaminationType
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<Examination> Examinations { get; set; } = new List<Examination>();
    public ICollection<ExaminationTypeTranslation> Translations { get; set; } = new List<ExaminationTypeTranslation>();
}
