using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class RolePermissionScope
{
    public int Id { get; set; }

    [Required]
    public int RolePermissionRuleId { get; set; }
    public RolePermissionRule RolePermissionRule { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public string ResourceType { get; set; } = string.Empty;

    [Required]
    public int ResourceId { get; set; }
}
