using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.Models;

public class VaccineType
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<Vaccine> Vaccines { get; set; } = new List<Vaccine>();
}
