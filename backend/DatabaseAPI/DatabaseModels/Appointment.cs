using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Appointment
{
    public int Id { get; set; }
    
    [Required]
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    
    [Required]
    public int EmployeeId { get; set; }
    public HospitalEmployee HospitalEmployee { get; set; } = null!;
    
    public int? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }
    
    public DateTime StartTime { get; set; }
    
    public DateTime EndTime { get; set; }
    
    [Required]
    public int HospitalId { get; set; }
    public Hospital Hospital { get; set; } = null!;
}
