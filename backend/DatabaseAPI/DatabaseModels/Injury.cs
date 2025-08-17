using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Injury
{
    public int Id { get; set; }
    
    [Required]
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    
    [Required]
    public int InjuryTypeId { get; set; }
    public InjuryType InjuryType { get; set; } = null!;
}
