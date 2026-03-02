namespace DatabaseAPI.DatabaseModels;

public class Hospital
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public bool? Active { get; set; }

    public int? AddressId { get; set; }
    public Address? Address { get; set; }

    public int? ParentHospitalId { get; set; }
    public Hospital? ParentHospital { get; set; }

    public string? CompanyIco { get; set; }

    public string? CompanyName { get; set; }

    // Navigation properties
    public ICollection<Hospital> ChildHospitals { get; set; } = new List<Hospital>();
    public ICollection<ContactToObject> ContactToObjects { get; set; } = new List<ContactToObject>();
    public ICollection<HospitalEquipment> HospitalEquipments { get; set; } = new List<HospitalEquipment>();
    public ICollection<HospitalEmployee> HospitalEmployees { get; set; } = new List<HospitalEmployee>();
    public ICollection<HospitalExaminationType> HospitalExaminationTypes { get; set; } = new List<HospitalExaminationType>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
