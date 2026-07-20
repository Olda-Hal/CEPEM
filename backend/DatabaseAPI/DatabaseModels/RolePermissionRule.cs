using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public enum AccessRuleEffect
{
    Allow = 1,
    Deny = 2
}

public class RolePermissionRule
{
    public int Id { get; set; }

    [Required]
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    [Required]
    [MaxLength(256)]
    public string PermissionKey { get; set; } = string.Empty;

    [Required]
    public AccessRuleEffect Effect { get; set; } = AccessRuleEffect.Allow;

    public ICollection<RolePermissionScope> Scopes { get; set; } = new List<RolePermissionScope>();
}
