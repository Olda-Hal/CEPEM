using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DatabaseAPI.DatabaseModels;

[Table("Reservations")]
public class Reservation
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ExaminationRoomId { get; set; }

    [ForeignKey(nameof(ExaminationRoomId))]
    public ExaminationRoom? ExaminationRoom { get; set; }

    [Required]
    public int DoctorId { get; set; }

    [ForeignKey(nameof(DoctorId))]
    public Employee? Doctor { get; set; }

    [Required]
    public int PersonId { get; set; }

    [ForeignKey(nameof(PersonId))]
    public Person? Person { get; set; }

    [Required]
    public int ExaminationTypeId { get; set; }

    [ForeignKey(nameof(ExaminationTypeId))]
    public ExaminationType? ExaminationType { get; set; }

    [Required]
    public DateTime StartDateTime { get; set; }

    [Required]
    public DateTime EndDateTime { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = "Scheduled";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
