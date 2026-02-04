using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DatabaseAPI.DatabaseModels;

[Table("DoctorExaminationRooms")]
public class DoctorExaminationRoom
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int DoctorId { get; set; }

    [ForeignKey(nameof(DoctorId))]
    public Employee? Doctor { get; set; }

    [Required]
    public int ExaminationRoomId { get; set; }

    [ForeignKey(nameof(ExaminationRoomId))]
    public ExaminationRoom? ExaminationRoom { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
