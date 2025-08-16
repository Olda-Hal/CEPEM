using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class InjuryType
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<Injury> Injuries { get; set; } = new List<Injury>();
}
