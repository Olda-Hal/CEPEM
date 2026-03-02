using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class ContactPhoneNumber
{
    public int Id { get; set; }

    [Required]
    public int ContactId { get; set; }
    public Contact Contact { get; set; } = null!;

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;
}
