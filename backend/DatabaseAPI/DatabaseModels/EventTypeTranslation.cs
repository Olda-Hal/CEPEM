using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class EventTypeTranslation
{
    public int Id { get; set; }
    
    [Required]
    public int EventTypeId { get; set; }
    
    [Required]
    [MaxLength(10)]
    public string Language { get; set; } = string.Empty;
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public EventType EventType { get; set; } = null!;
}
