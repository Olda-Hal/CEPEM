using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Address
{
    public int Id { get; set; }

    [Required]
    public string Street { get; set; } = string.Empty;

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    public string PostalCode { get; set; } = string.Empty;

    [Required]
    public string Country { get; set; } = string.Empty;

    // Navigation properties
    public Person? Person { get; set; }
    public Hospital? Hospital { get; set; }
}
