using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Vaccine
{
    public int Id { get; set; }
    
    [Required]
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    
    [Required]
    public int VaccineTypeId { get; set; }
    public VaccineType VaccineType { get; set; } = null!;
}
