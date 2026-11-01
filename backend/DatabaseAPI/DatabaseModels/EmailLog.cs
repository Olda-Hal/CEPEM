using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

/// <summary>
/// Audit trail of outgoing e-mails (password resets, intake-form reminders).
/// Only metadata is stored — never the reset/intake token or the message body.
/// </summary>
public class EmailLog
{
    public int Id { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(320)]
    public string ToAddress { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>PasswordReset / IntakeReminder (see <see cref="EmailLogKinds"/>).</summary>
    [Required]
    [StringLength(40)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Sent / LoggedOnly / Failed (see <see cref="EmailLogStatuses"/>).</summary>
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Error { get; set; }

    /// <summary>Recipient person (nullable; kept for filtering/linking).</summary>
    public int? PersonId { get; set; }

    /// <summary>Snapshot of the recipient name so the log survives deletions.</summary>
    [StringLength(200)]
    public string? RecipientName { get; set; }

    /// <summary>Set for intake-form reminders triggered by a reservation slot.</summary>
    public int? ReservationSlotId { get; set; }
}

public static class EmailLogKinds
{
    public const string PasswordReset = "PasswordReset";
    public const string IntakeReminder = "IntakeReminder";
}

public static class EmailLogStatuses
{
    /// <summary>The SMTP server accepted the message.</summary>
    public const string Sent = "Sent";

    /// <summary>SMTP is not configured; the message was only written to the log.</summary>
    public const string LoggedOnly = "LoggedOnly";

    /// <summary>Sending failed (the error is stored on the row).</summary>
    public const string Failed = "Failed";
}
