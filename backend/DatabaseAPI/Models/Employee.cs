using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class Employee
{
    public int Id { get; set; }
    
    [Required]
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    [Required]
    public string Salt { get; set; } = string.Empty;
    
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation properties
    public ICollection<HospitalEmployee> HospitalEmployees { get; set; } = new List<HospitalEmployee>();
}
