using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public static class CommunicationChannels
{
    public const string Email = "Email";
    public const string Sms = "Sms";
}

public static class CommunicationScopes
{
    public const string Global = "Global";
    public const string Country = "Country";
    public const string Center = "Center";
}

public static class CommunicationTemplateCodes
{
    public const string PasswordReset = "password_reset";
    public const string IntakeReminder = "intake_reminder";
    public const string ReservationConfirmed = "reservation_confirmed";
    public const string ReservationCancelled = "reservation_cancelled";
    public const string AppointmentReminderSms = "appointment_reminder";
}

/// <summary>
/// Editable e-mail/SMS wording. Resolution order is Center &gt; Country &gt;
/// Global for the same code, channel and language; ScopeId is 0 for Global,
/// the country scope id for Country and the hospital id for Center.
/// </summary>
public class CommunicationTemplate
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string Channel { get; set; } = CommunicationChannels.Email;

    [Required]
    [StringLength(5)]
    public string Language { get; set; } = "cs";

    [Required]
    [StringLength(10)]
    public string ScopeType { get; set; } = CommunicationScopes.Global;

    public int ScopeId { get; set; }

    [StringLength(300)]
    public string? Subject { get; set; }

    [Required]
    [StringLength(20000)]
    public string Body { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Delivery log for SMS messages (metadata only, never secrets).</summary>
public class SmsLog
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(40)]
    public string ToNumber { get; set; } = string.Empty;

    [StringLength(50)]
    public string TemplateCode { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Sent";

    [StringLength(1000)]
    public string? Error { get; set; }

    public int? PersonId { get; set; }

    [StringLength(200)]
    public string? RecipientName { get; set; }

    public int? ReservationSlotId { get; set; }
}
