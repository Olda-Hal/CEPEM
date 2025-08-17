using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Examination
{
    public int Id { get; set; }
    
    [Required]
    public int ExaminationTypeId { get; set; }
    public ExaminationType ExaminationType { get; set; } = null!;
    
    [Required]
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
}
