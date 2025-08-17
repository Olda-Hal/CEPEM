using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Pregnancy
{
    public int Id { get; set; }
    
    [Required]
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    
    public bool Result { get; set; }
}
