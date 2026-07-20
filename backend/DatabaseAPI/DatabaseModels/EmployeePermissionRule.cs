using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class EmployeePermissionRule
{
    public int Id { get; set; }

    [Required]
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    [Required]
    [MaxLength(256)]
    public string PermissionKey { get; set; } = string.Empty;

    [Required]
    public AccessRuleEffect Effect { get; set; } = AccessRuleEffect.Allow;

    public ICollection<EmployeePermissionScope> Scopes { get; set; } = new List<EmployeePermissionScope>();
}
