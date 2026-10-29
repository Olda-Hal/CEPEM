using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

/// <summary>
/// A patient visit ("návštěva"): a dated container for the visit note and
/// any number of documents (including the intake form event that produced
/// form submission data). Replaces the flat Documents/Events/Termíny view
/// on the patient page. Historical Events stay untouched; migrated visits
/// keep their lineage in SourceEventId.
/// </summary>
public class Visit
{
    public int Id { get; set; }

    [Required]
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    [Required]
    public DateTime VisitDate { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Lineage/explicit link to the intake-from-visit event (form submission).
    /// For migrated records this points at the original Event; no data moved.
    /// </summary>
    public int? SourceEventId { get; set; }
    public Event? SourceEvent { get; set; }

    public int? CreatedByEmployeeId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(2)]
    public string CountryCode { get; set; } = Patient.DefaultCountryCode;

    public ICollection<VisitDocument> Documents { get; set; } = new List<VisitDocument>();
}

/// <summary>Which patient documents are attached to a visit.</summary>
public class VisitDocument
{
    public int Id { get; set; }

    [Required]
    public int VisitId { get; set; }
    public Visit Visit { get; set; } = null!;

    [Required]
    public int PatientDocumentId { get; set; }
    public PatientDocument PatientDocument { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
