using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

/// <summary>
/// One-time, short-lived password reset token. Only a SHA-256 hash of the
/// generated token is stored — the plaintext exists solely in the e-mail
/// link sent to the employee. A token may be used exactly once.
/// </summary>
public class PasswordResetToken
{
    public int Id { get; set; }

    [Required]
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>SHA-256 hex digest of the token in the reset link.</summary>
    [Required]
    [StringLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Set once the token has been consumed or superseded by a newer request.</summary>
    public DateTime? UsedAtUtc { get; set; }
}
