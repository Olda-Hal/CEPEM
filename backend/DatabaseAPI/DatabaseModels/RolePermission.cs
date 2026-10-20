namespace DatabaseAPI.DatabaseModels;

public class RolePermission
{
    public int Id { get; set; }

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    // "Allow" or "Deny"
    public string Effect { get; set; } = "Allow";
}
