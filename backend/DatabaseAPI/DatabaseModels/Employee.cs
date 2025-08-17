using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Tracing;

namespace DatabaseAPI.DatabaseModels;

public class Employee
{
    public int Id { get; set; }
    
    [Required]
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime PasswordExpiration { get; set; }
    
    [Required]
    public string Salt { get; set; } = string.Empty;
    
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation properties
    public ICollection<HospitalEmployee> HospitalEmployees { get; set; } = new List<HospitalEmployee>();
}
