using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DatabaseAPI.DatabaseModels;

[Table("ExaminationRooms")]
public class ExaminationRoom
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    public int HospitalId { get; set; }

    [ForeignKey(nameof(HospitalId))]
    public Hospital? Hospital { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DoctorExaminationRoom> DoctorExaminationRooms { get; set; } = new List<DoctorExaminationRoom>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
