using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Translation
{
    public int Id { get; set; }

    [Required]
    public string EN { get; set; } = string.Empty;
    public string? CS { get; set; }
    public string? NL { get; set; }
}
