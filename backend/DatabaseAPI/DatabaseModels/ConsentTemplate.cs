using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

/// <summary>A versioned consent wording shown to patients (GDPR, treatment, ...).</summary>
public class ConsentTemplate
{
    public int Id { get; set; }

    [Required]
    [StringLength(40)]
    public string ConsentType { get; set; } = "gdpr";

    [Required]
    [StringLength(20)]
    public string Version { get; set; } = "1";

    [Required]
    [StringLength(5)]
    public string Language { get; set; } = "cs";

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(20000)]
    public string Text { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A recorded consent. Kept as a history row per patient/version/source; the
/// intake form creates one on every submission that accepts the terms.
/// </summary>
public class PatientConsent
{
    public int Id { get; set; }

    [Required]
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    [Required]
    [StringLength(40)]
    public string ConsentType { get; set; } = "gdpr";

    [Required]
    [StringLength(20)]
    public string Version { get; set; } = "1";

    [Required]
    [StringLength(5)]
    public string Language { get; set; } = "cs";

    [Required]
    [StringLength(30)]
    public string Source { get; set; } = "IntakeForm";

    public DateTime AcceptedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Intake event that carried the signature, when applicable.</summary>
    public int? IntakeEventId { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

/// <summary>Audit row for a GDPR export/anonymization action.</summary>
public class GdprRequest
{
    public int Id { get; set; }

    [Required]
    public int PatientId { get; set; }

    [Required]
    [StringLength(30)]
    public string RequestType { get; set; } = "Export";

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Completed";

    public int? RequestedByEmployeeId { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    [StringLength(2000)]
    public string? Details { get; set; }

    [StringLength(1000)]
    public string? Error { get; set; }
}
