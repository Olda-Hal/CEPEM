using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Patient
{
    public int Id { get; set; }
    
    [Required]
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    
    public DateTime BirthDate { get; set; }
    
    [Required]
    public int InsuranceNumber { get; set; }
    
    public int? CommentId { get; set; }
    public Comment? Comment { get; set; }
    
    public bool Alive { get; set; } = true;
    
    public string? PhotoPath { get; set; }
    
    // Navigation properties
    public ICollection<Event> Events { get; set; } = new List<Event>();
}
