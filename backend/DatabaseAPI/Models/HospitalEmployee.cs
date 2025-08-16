using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class HospitalEmployee
{
    public int Id { get; set; }
    
    [Required]
    public int HospitalId { get; set; }
    public Hospital Hospital { get; set; } = null!;
    
    [Required]
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    
    // Navigation properties
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
