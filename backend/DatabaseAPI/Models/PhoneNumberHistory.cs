using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class PhoneNumberHistory
{
    public int Id { get; set; }
    
    [Required]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required]
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    
    public DateTime UsedFrom { get; set; }
    
    public DateTime UsedTo { get; set; }
}
