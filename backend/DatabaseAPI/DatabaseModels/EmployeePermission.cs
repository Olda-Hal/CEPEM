using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class EmployeePermission
{
    public int Id { get; set; }

    // Reference to Person (user)
    [Required]
    public int UserId { get; set; }
    public Person User { get; set; } = null!;

    [Required]
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    // "Allow" or "Deny"
    public string Effect { get; set; } = "Allow";

    // Optional scope, e.g. "Hospital", "Department"
    public string? ScopeType { get; set; }
    public int? ScopeId { get; set; }
}
