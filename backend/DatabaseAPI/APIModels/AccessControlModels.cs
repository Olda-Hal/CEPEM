namespace DatabaseAPI.APIModels;

public class AccessResourceContextDto
{
    public string ResourceType { get; set; } = string.Empty;
    public int ResourceId { get; set; }
}

public class AccessEvaluationRequest
{
    public int EmployeeId { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
    public List<AccessResourceContextDto> ResourceContexts { get; set; } = new();
}

public class AccessEvaluationResponse
{
    public bool Allowed { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class PermissionRuleScopeDto
{
    public string ResourceType { get; set; } = string.Empty;
    public int ResourceId { get; set; }
}

public class PermissionRuleDto
{
    public string PermissionKey { get; set; } = string.Empty;
    public string Effect { get; set; } = "allow";
    public List<PermissionRuleScopeDto> Scopes { get; set; } = new();
}

public class EmployeePermissionRulesResponse
{
    public int EmployeeId { get; set; }
    public List<PermissionRuleDto> Rules { get; set; } = new();
    public int AccessControlVersion { get; set; }
}

public class UpdateEmployeePermissionRulesRequest
{
    public List<PermissionRuleDto> Rules { get; set; } = new();
}

public class RolePermissionRulesResponse
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public List<PermissionRuleDto> Rules { get; set; } = new();
}

public class UpdateRolePermissionRulesRequest
{
    public List<PermissionRuleDto> Rules { get; set; } = new();
}

public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
}
