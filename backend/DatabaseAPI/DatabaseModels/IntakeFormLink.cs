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

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
