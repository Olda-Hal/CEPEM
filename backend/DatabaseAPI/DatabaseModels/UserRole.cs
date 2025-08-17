using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class UserRole
{
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    public Person User { get; set; } = null!;
    
    [Required]
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
