using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class FirstNameHistory
{
    public int Id { get; set; }
    
    [Required]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    
    public DateTime UsedFrom { get; set; }
    
    public DateTime UsedTo { get; set; }
}
