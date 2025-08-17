using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class HospitalEquipment
{
    public int Id { get; set; }
    
    [Required]
    public int HospitalId { get; set; }
    public Hospital Hospital { get; set; } = null!;
    
    [Required]
    public int EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
}
