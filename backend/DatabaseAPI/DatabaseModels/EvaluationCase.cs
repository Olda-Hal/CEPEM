using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

/// <summary>
/// Mock of the doctor-evaluation queue (Vyhodnocování): one record per
/// image-like measurement to be reviewed by an evaluating doctor. Markers
/// clicked onto the image are stored as JSON so the shape stays open for the
/// follow-up flow without another migration.
/// </summary>
public class EvaluationCase
{
    public int Id { get; set; }

    /// <summary>Optional link to a real patient in demo/seeded data.</summary>
    public int? PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>Source .tdb document when the case came from an upload.</summary>
    public int? PatientDocumentId { get; set; }
    public PatientDocument? PatientDocument { get; set; }

    /// <summary>Visit the measurement was uploaded to (interactive report lives there).</summary>
    public int? VisitId { get; set; }
    public Visit? Visit { get; set; }

    /// <summary>Independent evaluations (up to two different doctors).</summary>
    public ICollection<EvaluationReview> Reviews { get; set; } = new List<EvaluationReview>();

    /// <summary>Parsed measurement conditions from the .tdb header ("Left"/"Right").</summary>
    public string? SourceSide { get; set; }

    /// <summary>Parsed posture from the .tdb header ("Lying"/"Standing"/"Sitting").</summary>
    public string? SourcePosture { get; set; }

    /// <summary>Raw measurement conditions text (MEIK "Measurement conditions").</summary>
    public string? SourceConditions { get; set; }

    /// <summary>Serialized TDB slices (7 x 36 x 36 float32 grids + histograms).</summary>
    public string? SlicesJson { get; set; }

    /// <summary>Doctor's verdict: "OK" (no findings) or "Findings".</summary>
    public string? Outcome { get; set; }

    /// <summary>Display label of the examination, e.g. "RTG hrudníku".</summary>
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Key of the built-in mock image shown in the UI (e.g. "chest").</summary>
    [Required]
    [StringLength(64)]
    public string ImageKey { get; set; } = string.Empty;

    /// <summary>JSON array of markers: [{ "x": 0.42, "y": 0.31, "note": "…" }] (normalized coords).</summary>
    public string? AnnotationsJson { get; set; }

    public string? Recommendation { get; set; }

    public int? EvaluatedByEmployeeId { get; set; }
    public Employee? EvaluatedByEmployee { get; set; }

    public DateTime? EvaluatedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsEvaluated => EvaluatedAtUtc != null;
}
