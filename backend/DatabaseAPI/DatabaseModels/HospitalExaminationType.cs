using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class HospitalExaminationType
{
    public int Id { get; set; }

    [Required]
    public int HospitalId { get; set; }
    public Hospital Hospital { get; set; } = null!;

    [Required]
    public int ExaminationTypeId { get; set; }
    public ExaminationType ExaminationType { get; set; } = null!;
}
