using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class DrugUse
{
    public int Id { get; set; }
    
    [Required]
    public int DrugId { get; set; }
    public Drug Drug { get; set; } = null!;
    
    [Required]
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
}
