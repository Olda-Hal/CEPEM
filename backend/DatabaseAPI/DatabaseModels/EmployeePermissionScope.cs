using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class EmployeePermissionScope
{
    public int Id { get; set; }

    [Required]
    public int EmployeePermissionRuleId { get; set; }
    public EmployeePermissionRule EmployeePermissionRule { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public string ResourceType { get; set; } = string.Empty;

    [Required]
    public int ResourceId { get; set; }
}
