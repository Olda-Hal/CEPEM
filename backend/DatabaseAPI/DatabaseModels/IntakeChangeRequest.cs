using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

/// <summary>
/// Master-data changes submitted through the public intake link. They wait for
/// staff approval instead of overwriting the patient card directly.
/// </summary>
public class IntakeChangeRequest
{
    public int Id { get; set; }

    [Required]
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>Intake event that carried the proposed values.</summary>
    public int? IntakeEventId { get; set; }
    public Event? IntakeEvent { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = IntakeChangeStatuses.Pending;

    /// <summary>Serialized CreateIntakeFormEventRequest with the proposed values.</summary>
    [Required]
    public string ChangesJson { get; set; } = "{}";

    [StringLength(2000)]
    public string? Summary { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }

    public int? ReviewedByEmployeeId { get; set; }
}

public static class IntakeChangeStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
