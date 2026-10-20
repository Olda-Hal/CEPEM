namespace DatabaseAPI.DatabaseModels;

public class Permission
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // e.g. patient.read
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<EmployeePermission> EmployeePermissions { get; set; } = new List<EmployeePermission>();
}
