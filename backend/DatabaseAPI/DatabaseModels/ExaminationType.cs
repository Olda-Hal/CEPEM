namespace DatabaseAPI.DatabaseModels;

public class ExaminationType
{
    public int Id { get; set; }

    public int? NameTranslationId { get; set; }
    public Translation? NameTranslation { get; set; }

    // Navigation properties
    public ICollection<Examination> Examinations { get; set; } = new List<Examination>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<HospitalExaminationType> HospitalExaminationTypes { get; set; } = new List<HospitalExaminationType>();
}
