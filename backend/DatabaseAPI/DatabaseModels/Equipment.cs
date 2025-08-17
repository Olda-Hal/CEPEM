using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Equipment
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<HospitalEquipment> HospitalEquipments { get; set; } = new List<HospitalEquipment>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
