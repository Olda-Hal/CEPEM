namespace DatabaseAPI.DatabaseModels;

public class Hospital
{
    public int Id { get; set; }
    
    public string? Address { get; set; }
    
    public bool? Active { get; set; }
    
    // Navigation properties
    public ICollection<HospitalEquipment> HospitalEquipments { get; set; } = new List<HospitalEquipment>();
    public ICollection<HospitalEmployee> HospitalEmployees { get; set; } = new List<HospitalEmployee>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
