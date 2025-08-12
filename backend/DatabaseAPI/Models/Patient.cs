using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class Patient
{
    public int Id { get; set; }
    
    [Required]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    public string LastName { get; set; } = string.Empty;
    
    [EmailAddress]
    public string? Email { get; set; }
    
    public string? PhoneNumber { get; set; }
    
    public DateTime? DateOfBirth { get; set; }
    
    public string? Gender { get; set; }
    
    public string? Address { get; set; }
    
    public string? InsuranceNumber { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    public bool IsActive { get; set; } = true;
}
