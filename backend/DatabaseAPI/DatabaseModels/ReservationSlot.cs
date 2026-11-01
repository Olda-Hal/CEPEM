using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DatabaseAPI.DatabaseModels;

[Table("ReservationSlots")]
public class ReservationSlot
{
    public const string DefaultCountryCode = "CZ";

    [Key]
    public int Id { get; set; }

    [Required]
    public int HospitalId { get; set; }

    [ForeignKey(nameof(HospitalId))]
    public Hospital? Hospital { get; set; }

    public int? DoctorId { get; set; }

    [ForeignKey(nameof(DoctorId))]
    public Employee? Doctor { get; set; }

    public int? PersonId { get; set; }

    [ForeignKey(nameof(PersonId))]
    public Person? Person { get; set; }

    public int? ExaminationTypeId { get; set; }

    [ForeignKey(nameof(ExaminationTypeId))]
    public ExaminationType? ExaminationType { get; set; }

    [Required]
    public DateTime StartDateTime { get; set; }

    [Required]
    public DateTime EndDateTime { get; set; }

    [StringLength(1000)]
    public string? PublicNote { get; set; }

    [StringLength(1000)]
    public string? InternalNote { get; set; }

    [Required]
    [StringLength(50)]
    public SlotStatus Status { get; set; } = SlotStatus.Available;

    [Required]
    [StringLength(2)]
    public string CountryCode { get; set; } = DefaultCountryCode;

    /// <summary>
    /// Set when the "fill in your intake form" reminder e-mail has been sent
    /// for this confirmed slot (also acts as the reminder idempotency key).
    /// </summary>
    public DateTime? IntakeReminderSentAtUtc { get; set; }

    /// <summary>Set when the day-before SMS reminder has been sent.</summary>
    public DateTime? SmsReminderSentAtUtc { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}