using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Patient
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public const string DefaultCountryCode = "CZ";

    public int Id { get; set; }
    
    [Required]
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    
    public DateTime BirthDate { get; set; }
    
    [Required]
    public int InsuranceNumber { get; set; }
    
    public int? CommentId { get; set; }
    public Comment? Comment { get; set; }
    
    public bool Alive { get; set; } = true;

    [Required]
    [StringLength(2)]
    public string CountryCode { get; set; } = DefaultCountryCode;

    /// <summary>
    /// Home center of the patient — assigned automatically from the creating
    /// employee's center. Nullable for historical records (pre-migration).
    /// </summary>
    public int? HomeHospitalId { get; set; }
    public Hospital? HomeHospital { get; set; }

    public string? PhotoPath { get; set; }

    // Soft-delete ("hide from the listing") — records stay in the table for
    // history/legal trail; queries filter them out by default.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // Navigation properties
    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<FormSubmission> FormSubmissions { get; set; } = [];
    public ICollection<PatientDocument> Documents { get; set; } = new List<PatientDocument>();
}
