using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

/// <summary>
/// One doctor's independent evaluation of a measurement. A measurement can
/// collect at most two reviews by different doctors; the second evaluator
/// never sees the first review.
/// </summary>
public class EvaluationReview
{
    public int Id { get; set; }

    [Required]
    public int EvaluationCaseId { get; set; }
    public EvaluationCase EvaluationCase { get; set; } = null!;

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    /// <summary>JSON array of [{ slice, x, y, note }] findings.</summary>
    public string? AnnotationsJson { get; set; }

    public string? Recommendation { get; set; }

    /// <summary>"OK" (no findings) or "Findings".</summary>
    public string? Outcome { get; set; }

    public DateTime EvaluatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
