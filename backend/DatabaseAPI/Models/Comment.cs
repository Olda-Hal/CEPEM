using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class Comment
{
    public int Id { get; set; }
    
    [Required]
    public string Text { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<Person> Persons { get; set; } = new List<Person>();
    public ICollection<Patient> Patients { get; set; } = new List<Patient>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
}
