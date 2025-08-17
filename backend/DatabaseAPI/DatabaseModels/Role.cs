using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Role
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
