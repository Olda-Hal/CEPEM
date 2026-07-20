using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Examination
{
    public const string DefaultCountryCode = "CZ";

    public int Id { get; set; }
    
    [Required]
    public int ExaminationTypeId { get; set; }
    public ExaminationType ExaminationType { get; set; } = null!;
    
    [Required]
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    [Required]
    [StringLength(2)]
    public string CountryCode { get; set; } = DefaultCountryCode;

    public ICollection<ExaminationDocument> Documents { get; set; } = [];
}
