using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Repositories;

public interface IAccessControlRepository
{
    Task<Employee?> GetEmployeeWithRolesAsync(int employeeId);
    Task<Role?> GetRoleWithTranslationAsync(int roleId);
    Task<int?> GetHospitalIdForReservationSlotAsync(int slotId);
    Task<int?> GetHospitalIdForExaminationRoomAsync(int roomId);
    Task<int?> GetHospitalIdForDoctorRoomAssignmentAsync(int assignmentId);
    Task<int?> GetCountryScopeIdForHospitalAsync(int hospitalId);
    Task<int?> GetCountryScopeIdForReservationSlotAsync(int slotId);
    Task<int?> GetCountryScopeIdForExaminationRoomAsync(int roomId);
    Task<int?> GetCountryScopeIdForDoctorRoomAssignmentAsync(int assignmentId);
    Task<List<EmployeePermissionRule>> GetEmployeePermissionRulesAsync(int employeeId);
    Task<List<RolePermissionRule>> GetRolePermissionRulesAsync(List<int> roleIds);
    Task<List<RolePermissionRule>> GetRolePermissionRulesByRoleIdAsync(int roleId);
    Task ReplaceEmployeeRulesAsync(int employeeId, List<PermissionRuleDto> rules);
    Task ReplaceRoleRulesAsync(int roleId, List<PermissionRuleDto> rules);
    Task SaveChangesAsync();
}

public class AccessControlRepository : IAccessControlRepository
{
    private readonly DatabaseContext _context;

    public AccessControlRepository(DatabaseContext context)
    {
        _context = context;
    }

    public Task<Employee?> GetEmployeeWithRolesAsync(int employeeId)
    {
        return _context.Employees
            .Include(e => e.Person)
                .ThenInclude(p => p.UserRoles)
            .FirstOrDefaultAsync(e => e.Id == employeeId);
    }

    public Task<Role?> GetRoleWithTranslationAsync(int roleId)
    {
        return _context.Roles
            .Include(r => r.NameTranslation)
            .FirstOrDefaultAsync(r => r.Id == roleId);
    }

    public Task<int?> GetHospitalIdForReservationSlotAsync(int slotId)
    {
        return _context.ReservationSlots
            .Where(slot => slot.Id == slotId)
            .Select(slot => (int?)slot.HospitalId)
            .FirstOrDefaultAsync();
    }

    public Task<int?> GetHospitalIdForExaminationRoomAsync(int roomId)
    {
        return _context.ExaminationRooms
            .Where(room => room.Id == roomId)
            .Select(room => (int?)room.HospitalId)
            .FirstOrDefaultAsync();
    }

    public Task<int?> GetHospitalIdForDoctorRoomAssignmentAsync(int assignmentId)
    {
        return _context.DoctorExaminationRooms
            .Where(assignment => assignment.Id == assignmentId)
            .Select(assignment => (int?)assignment.ExaminationRoom!.HospitalId)
            .FirstOrDefaultAsync();
    }

    public Task<int?> GetCountryScopeIdForHospitalAsync(int hospitalId)
    {
        return _context.Hospitals
            .Where(hospital => hospital.Id == hospitalId)
            .Select(hospital => (int?)hospital.CountryScopeId)
            .FirstOrDefaultAsync();
    }

    public Task<int?> GetCountryScopeIdForReservationSlotAsync(int slotId)
    {
        return _context.ReservationSlots
            .Where(slot => slot.Id == slotId)
            .Select(slot => (int?)slot.Hospital!.CountryScopeId)
            .FirstOrDefaultAsync();
    }

    public Task<int?> GetCountryScopeIdForExaminationRoomAsync(int roomId)
    {
        return _context.ExaminationRooms
            .Where(room => room.Id == roomId)
            .Select(room => (int?)room.Hospital!.CountryScopeId)
            .FirstOrDefaultAsync();
    }

    public Task<int?> GetCountryScopeIdForDoctorRoomAssignmentAsync(int assignmentId)
    {
        return _context.DoctorExaminationRooms
            .Where(assignment => assignment.Id == assignmentId)
            .Select(assignment => (int?)assignment.ExaminationRoom!.Hospital!.CountryScopeId)
            .FirstOrDefaultAsync();
    }

    public Task<List<EmployeePermissionRule>> GetEmployeePermissionRulesAsync(int employeeId)
    {
        return _context.EmployeePermissionRules
            .Include(r => r.Scopes)
            .Where(r => r.EmployeeId == employeeId)
            .ToListAsync();
    }

    public Task<List<RolePermissionRule>> GetRolePermissionRulesAsync(List<int> roleIds)
    {
        return _context.RolePermissionRules
            .Include(r => r.Scopes)
            .Where(r => roleIds.Contains(r.RoleId))
            .ToListAsync();
    }

    public Task<List<RolePermissionRule>> GetRolePermissionRulesByRoleIdAsync(int roleId)
    {
        return _context.RolePermissionRules
            .Include(r => r.Scopes)
            .Where(r => r.RoleId == roleId)
            .ToListAsync();
    }

    public async Task ReplaceEmployeeRulesAsync(int employeeId, List<PermissionRuleDto> rules)
    {
        var existing = await _context.EmployeePermissionRules
            .Include(r => r.Scopes)
            .Where(r => r.EmployeeId == employeeId)
            .ToListAsync();

        _context.EmployeePermissionRules.RemoveRange(existing);

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.PermissionKey))
                continue;

            var effect = rule.Effect.Equals("deny", StringComparison.OrdinalIgnoreCase)
                ? AccessRuleEffect.Deny
                : AccessRuleEffect.Allow;

            var dbRule = new EmployeePermissionRule
            {
                EmployeeId = employeeId,
                PermissionKey = rule.PermissionKey.Trim(),
                Effect = effect,
                Scopes = rule.Scopes
                    .Where(s => !string.IsNullOrWhiteSpace(s.ResourceType) && s.ResourceId > 0)
                    .Select(s => new EmployeePermissionScope
                    {
                        ResourceType = s.ResourceType.Trim(),
                        ResourceId = s.ResourceId
                    })
                    .ToList()
            };

            _context.EmployeePermissionRules.Add(dbRule);
        }
    }

    public async Task ReplaceRoleRulesAsync(int roleId, List<PermissionRuleDto> rules)
    {
        var existing = await _context.RolePermissionRules
            .Include(r => r.Scopes)
            .Where(r => r.RoleId == roleId)
            .ToListAsync();

        _context.RolePermissionRules.RemoveRange(existing);

        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.PermissionKey))
                continue;

            var effect = rule.Effect.Equals("deny", StringComparison.OrdinalIgnoreCase)
                ? AccessRuleEffect.Deny
                : AccessRuleEffect.Allow;

            var dbRule = new RolePermissionRule
            {
                RoleId = roleId,
                PermissionKey = rule.PermissionKey.Trim(),
                Effect = effect,
                Scopes = rule.Scopes
                    .Where(s => !string.IsNullOrWhiteSpace(s.ResourceType) && s.ResourceId >= 0)
                    .Select(s => new RolePermissionScope
                    {
                        ResourceType = s.ResourceType.Trim(),
                        ResourceId = s.ResourceId
                    })
                    .ToList()
            };

            _context.RolePermissionRules.Add(dbRule);
        }
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
