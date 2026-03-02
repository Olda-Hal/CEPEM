using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class ContactEmail
{
    public int Id { get; set; }

    [Required]
    public int ContactId { get; set; }
    public Contact Contact { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
