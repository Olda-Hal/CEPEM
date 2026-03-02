namespace DatabaseAPI.DatabaseModels;

public class InjuryType
{
    public int Id { get; set; }

    public int? NameTranslationId { get; set; }
    public Translation? NameTranslation { get; set; }

    // Navigation properties
    public ICollection<Injury> Injuries { get; set; } = new List<Injury>();
}
