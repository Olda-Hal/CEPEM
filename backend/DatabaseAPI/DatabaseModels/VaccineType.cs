namespace DatabaseAPI.DatabaseModels;

public class VaccineType
{
    public int Id { get; set; }

    public int? NameTranslationId { get; set; }
    public Translation? NameTranslation { get; set; }

    // Navigation properties
    public ICollection<Vaccine> Vaccines { get; set; } = new List<Vaccine>();
}
