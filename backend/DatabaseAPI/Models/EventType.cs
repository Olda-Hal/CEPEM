using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class EventType
{
    public int Id { get; set; }
    
    public int? Name { get; set; }
    
    // Navigation properties
    public ICollection<Event> Events { get; set; } = new List<Event>();
}
