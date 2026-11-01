using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class IntakeFormLink
{
    public int Id { get; set; }

    [Required]
    [StringLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    [Required]
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;

    public int? ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    /// <summary>
    /// Reservation slot this form belongs to (the per-visit flow). Links tied
    /// to a slot or a visit stay editable until they expire; legacy links
    /// without either remain single-use.
    /// </summary>
    public int? ReservationSlotId { get; set; }
    public ReservationSlot? ReservationSlot { get; set; }

    /// <summary>Visit the form is attached to (set for tablet/staff links).</summary>
    public int? VisitId { get; set; }
    public Visit? Visit { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>First submission; kept for audit (may be followed by edits).</summary>
    public DateTime? UsedAtUtc { get; set; }

    /// <summary>Last submission, including edits of an already submitted form.</summary>
    public DateTime? LastSubmittedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Per-visit links may be resubmitted until they expire.</summary>
    public bool AllowsEdits => ReservationSlotId.HasValue || VisitId.HasValue;
}
