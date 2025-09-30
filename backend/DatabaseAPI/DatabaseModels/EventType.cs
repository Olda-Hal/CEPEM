using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class EventType
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<Event> Events { get; set; } = new List<Event>();
}
