using DatabaseAPI.APIModels;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Repositories;

namespace DatabaseAPI.Services;

public interface IAccessControlService
{
    Task<AccessEvaluationResponse> EvaluateAsync(AccessEvaluationRequest request);
    Task<EmployeePermissionRulesResponse?> GetEmployeeRulesAsync(int employeeId);
    Task<bool> UpdateEmployeeRulesAsync(int employeeId, UpdateEmployeePermissionRulesRequest request);
    Task<RolePermissionRulesResponse?> GetRoleRulesAsync(int roleId);
    Task<bool> UpdateRoleRulesAsync(int roleId, UpdateRolePermissionRulesRequest request);
    Task<int?> GetHospitalIdForReservationSlotAsync(int slotId);
    Task<int?> GetHospitalIdForExaminationRoomAsync(int roomId);
    Task<int?> GetHospitalIdForDoctorRoomAssignmentAsync(int assignmentId);
    Task<int?> GetCountryScopeIdForHospitalAsync(int hospitalId);
    Task<int?> GetCountryScopeIdForReservationSlotAsync(int slotId);
    Task<int?> GetCountryScopeIdForExaminationRoomAsync(int roomId);
    Task<int?> GetCountryScopeIdForDoctorRoomAssignmentAsync(int assignmentId);
}

public class AccessControlService : IAccessControlService
{
    private readonly IAccessControlRepository _repository;

    public AccessControlService(IAccessControlRepository repository)
    {
        _repository = repository;
    }

    public async Task<AccessEvaluationResponse> EvaluateAsync(AccessEvaluationRequest request)
    {
        var employee = await _repository.GetEmployeeWithRolesAsync(request.EmployeeId);
        if (employee == null || employee.Person.Active == false)
            return new AccessEvaluationResponse { Allowed = false, Reason = "Employee not found or inactive" };

        var employeeRules = await _repository.GetEmployeePermissionRulesAsync(request.EmployeeId);
        var roleIds = employee.Person.UserRoles.Select(ur => ur.RoleId).ToList();
        var roleRules = roleIds.Count == 0
            ? new List<RolePermissionRule>()
            : await _repository.GetRolePermissionRulesAsync(roleIds);

        if (MatchesDeniedRule(employeeRules, request.PermissionKey, request.ResourceContexts))
            return new AccessEvaluationResponse { Allowed = false, Reason = "Explicit user deny" };

        if (MatchesAllowedRule(employeeRules, request.PermissionKey, request.ResourceContexts))
            return new AccessEvaluationResponse { Allowed = true, Reason = "Explicit user allow" };

        if (MatchesDeniedRule(roleRules, request.PermissionKey, request.ResourceContexts))
            return new AccessEvaluationResponse { Allowed = false, Reason = "Role deny" };

        if (MatchesAllowedRule(roleRules, request.PermissionKey, request.ResourceContexts))
            return new AccessEvaluationResponse { Allowed = true, Reason = "Role allow" };

        return new AccessEvaluationResponse { Allowed = false, Reason = "Default deny" };
    }

    public async Task<EmployeePermissionRulesResponse?> GetEmployeeRulesAsync(int employeeId)
    {
        var employee = await _repository.GetEmployeeWithRolesAsync(employeeId);
        if (employee == null)
            return null;

        var rules = await _repository.GetEmployeePermissionRulesAsync(employeeId);
        return new EmployeePermissionRulesResponse
        {
            EmployeeId = employeeId,
            AccessControlVersion = employee.AccessControlVersion,
            Rules = rules.Select(r => new PermissionRuleDto
            {
                PermissionKey = r.PermissionKey,
                Effect = r.Effect == AccessRuleEffect.Deny ? "deny" : "allow",
                Scopes = r.Scopes.Select(s => new PermissionRuleScopeDto
                {
                    ResourceType = s.ResourceType,
                    ResourceId = s.ResourceId
                }).ToList()
            }).ToList()
        };
    }

    public async Task<bool> UpdateEmployeeRulesAsync(int employeeId, UpdateEmployeePermissionRulesRequest request)
    {
        var employee = await _repository.GetEmployeeWithRolesAsync(employeeId);
        if (employee == null)
            return false;

        await _repository.ReplaceEmployeeRulesAsync(employeeId, request.Rules);
        employee.AccessControlVersion += 1;
        await _repository.SaveChangesAsync();
        return true;
    }

    public async Task<RolePermissionRulesResponse?> GetRoleRulesAsync(int roleId)
    {
        var role = await _repository.GetRoleWithTranslationAsync(roleId);
        if (role == null)
            return null;

        var rules = await _repository.GetRolePermissionRulesByRoleIdAsync(roleId);
        return new RolePermissionRulesResponse
        {
            RoleId = roleId,
            RoleName = role.NameTranslation?.EN ?? string.Empty,
            Rules = rules.Select(r => new PermissionRuleDto
            {
                PermissionKey = r.PermissionKey,
                Effect = r.Effect == AccessRuleEffect.Deny ? "deny" : "allow",
                Scopes = r.Scopes.Select(s => new PermissionRuleScopeDto
                {
                    ResourceType = s.ResourceType,
                    ResourceId = s.ResourceId
                }).ToList()
            }).ToList()
        };
    }

    public async Task<bool> UpdateRoleRulesAsync(int roleId, UpdateRolePermissionRulesRequest request)
    {
        var role = await _repository.GetRoleWithTranslationAsync(roleId);
        if (role == null)
            return false;

        await _repository.ReplaceRoleRulesAsync(roleId, request.Rules);
        await _repository.SaveChangesAsync();
        return true;
    }

    public Task<int?> GetHospitalIdForReservationSlotAsync(int slotId)
        => _repository.GetHospitalIdForReservationSlotAsync(slotId);

    public Task<int?> GetHospitalIdForExaminationRoomAsync(int roomId)
        => _repository.GetHospitalIdForExaminationRoomAsync(roomId);

    public Task<int?> GetHospitalIdForDoctorRoomAssignmentAsync(int assignmentId)
        => _repository.GetHospitalIdForDoctorRoomAssignmentAsync(assignmentId);

    public Task<int?> GetCountryScopeIdForHospitalAsync(int hospitalId)
        => _repository.GetCountryScopeIdForHospitalAsync(hospitalId);

    public Task<int?> GetCountryScopeIdForReservationSlotAsync(int slotId)
        => _repository.GetCountryScopeIdForReservationSlotAsync(slotId);

    public Task<int?> GetCountryScopeIdForExaminationRoomAsync(int roomId)
        => _repository.GetCountryScopeIdForExaminationRoomAsync(roomId);

    public Task<int?> GetCountryScopeIdForDoctorRoomAssignmentAsync(int assignmentId)
        => _repository.GetCountryScopeIdForDoctorRoomAssignmentAsync(assignmentId);

    private static bool MatchesDeniedRule(IEnumerable<EmployeePermissionRule> rules, string permissionKey, List<AccessResourceContextDto> contexts)
        => MatchesRule(rules, permissionKey, AccessRuleEffect.Deny, contexts);

    private static bool MatchesAllowedRule(IEnumerable<EmployeePermissionRule> rules, string permissionKey, List<AccessResourceContextDto> contexts)
        => MatchesRule(rules, permissionKey, AccessRuleEffect.Allow, contexts);

    private static bool MatchesDeniedRule(IEnumerable<RolePermissionRule> rules, string permissionKey, List<AccessResourceContextDto> contexts)
        => MatchesRule(rules, permissionKey, AccessRuleEffect.Deny, contexts);

    private static bool MatchesAllowedRule(IEnumerable<RolePermissionRule> rules, string permissionKey, List<AccessResourceContextDto> contexts)
        => MatchesRule(rules, permissionKey, AccessRuleEffect.Allow, contexts);

    private static bool MatchesRule(IEnumerable<EmployeePermissionRule> rules, string permissionKey, AccessRuleEffect effect, List<AccessResourceContextDto> contexts)
    {
        return rules.Any(r =>
            PermissionMatches(r.PermissionKey, permissionKey) &&
            r.Effect == effect &&
            ScopeMatches(r.Scopes.Select(s => new PermissionRuleScopeDto { ResourceType = s.ResourceType, ResourceId = s.ResourceId }).ToList(), contexts));
    }

    private static bool MatchesRule(IEnumerable<RolePermissionRule> rules, string permissionKey, AccessRuleEffect effect, List<AccessResourceContextDto> contexts)
    {
        return rules.Any(r =>
            PermissionMatches(r.PermissionKey, permissionKey) &&
            r.Effect == effect &&
            ScopeMatches(r.Scopes.Select(s => new PermissionRuleScopeDto { ResourceType = s.ResourceType, ResourceId = s.ResourceId }).ToList(), contexts));
    }

    private static bool PermissionMatches(string pattern, string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        if (pattern == "*")
            return true;

        if (pattern.EndsWith("*", StringComparison.Ordinal))
        {
            var prefix = pattern[..^1];
            return permissionKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return pattern.Equals(permissionKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ScopeMatches(List<PermissionRuleScopeDto> scopes, List<AccessResourceContextDto> contexts)
    {
        if (scopes.Count == 0)
            return true;

        if (contexts.Count == 0)
            return false;

        return scopes.Any(scope => contexts.Any(ctx =>
            scope.ResourceType.Equals(ctx.ResourceType, StringComparison.OrdinalIgnoreCase) &&
            scope.ResourceId == ctx.ResourceId));
    }
}
