using DatabaseAPI.Data;
using DatabaseAPI.Utils;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

/// <summary>Thrown when the acting employee's administrative scope forbids an operation.</summary>
public class ScopeViolationException : Exception
{
    public ScopeViolationException(string message) : base(message) { }
}

/// <summary>
/// Resolved administrative scope of the acting employee (from the
/// X-Actor-Employee-Id header the gateway forwards). SysAdmin/Admin always
/// have full scope; Country Admin is scoped to their administered countries
/// (their own country + countries of centers they are assigned to); Center
/// Admin is scoped to their assigned centers.
/// </summary>
public record ActorScope(
    int? EmployeeId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<int> HospitalIds,
    IReadOnlyList<int> AdministeredCountryScopeIds)
{
    public bool HasFullAccess => Roles.Contains("SysAdmin") || Roles.Contains("Admin");
    public bool IsCountryAdmin => Roles.Contains("Country Admin");
    public bool IsCenterAdmin => Roles.Contains("Center Admin");

    /// <summary>Whether the scoping restrictions apply to this actor at all.</summary>
    public bool IsScopedAdmin => !HasFullAccess && (IsCountryAdmin || IsCenterAdmin);

    /// <summary>Only Center Admin and above may see/list hidden (soft-deleted) patients.</summary>
    public bool CanSeeDeletedPatients => HasFullAccess || IsCountryAdmin || IsCenterAdmin;

    /// <summary>
    /// Role names this actor may grant to others:
    /// SysAdmin/Admin → any; Country Admin → Center Admin and below;
    /// Center Admin → only Examiner/Doctor.
    /// </summary>
    public IReadOnlyList<string> AssignableRoleNames =>
        HasFullAccess ? new List<string>()
                        : IsCountryAdmin ? new List<string> { "Center Admin", "Examiner", "Doctor" }
                        : IsCenterAdmin ? new List<string> { "Examiner", "Doctor" }
                        : new List<string>();
}

public interface IEmployeeScopeService
{
    Task<ActorScope> GetActorScopeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// SQL-composable predicate source: employee ids visible to the actor.
    /// Returns null when the actor sees everyone (full access or no admin role).
    /// </summary>
    Task<HashSet<int>?> GetVisibleEmployeeIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Null when the actor may assign the given roles; otherwise a human
    /// readable reason why not.
    /// </summary>
    Task<string?> ValidateAssignableRolesAsync(IReadOnlyList<int> roleIds, CancellationToken cancellationToken = default);

    /// <summary>Null when valid; a reason string otherwise.</summary>
    Task<string?> ValidateCountryAssignmentAsync(string? countryCode, CancellationToken cancellationToken = default);

    /// <summary>Null when valid; a reason string otherwise.</summary>
    Task<string?> ValidateHospitalAssignmentAsync(IReadOnlyList<int> hospitalIds, CancellationToken cancellationToken = default);
}

public class EmployeeScopeService : IEmployeeScopeService
{
    private readonly DatabaseContext _context;
    private readonly IActorCountryContextService _actorCountryContextService;

    public EmployeeScopeService(DatabaseContext context, IActorCountryContextService actorCountryContextService)
    {
        _context = context;
        _actorCountryContextService = actorCountryContextService;
    }

    public async Task<ActorScope> GetActorScopeAsync(CancellationToken cancellationToken = default)
    {
        var employeeId = _actorCountryContextService.GetActorEmployeeId();
        if (employeeId == null)
        {
            return new ActorScope(null, [], [], []);
        }

        var employeeData = await _context.Employees
            .Where(e => e.Id == employeeId.Value)
            .Select(e => new
            {
                CountryCode = e.Person.CountryCode,
                Roles = e.Person.UserRoles
                    .Select(ur => ur.Role.NameTranslation != null ? ur.Role.NameTranslation.EN : "")
                    .Where(name => name != string.Empty)
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (employeeData == null)
        {
            return new ActorScope(employeeId, [], [], []);
        }

        var hospitalIds = await _context.HospitalEmployees
            .Where(he => he.EmployeeId == employeeId.Value)
            .Select(he => he.HospitalId)
            .ToListAsync(cancellationToken);

        var hospitalScopes = hospitalIds.Count == 0
            ? new List<int>()
            : await _context.Hospitals
                .Where(h => hospitalIds.Contains(h.Id))
                .Select(h => h.CountryScopeId)
                .ToListAsync(cancellationToken);

        var administeredScopes = hospitalScopes
            .Append(CountryScopeMapper.ToScopeId(employeeData.CountryCode))
            .Distinct()
            .ToList();

        return new ActorScope(
            employeeId,
            employeeData.Roles,
            hospitalIds,
            administeredScopes);
    }

    public async Task<HashSet<int>?> GetVisibleEmployeeIdsAsync(CancellationToken cancellationToken = default)
    {
        var actor = await GetActorScopeAsync(cancellationToken);
        if (!actor.IsScopedAdmin)
        {
            return null; // no filtering
        }

        var visible = new HashSet<int>();

        if (actor.IsCountryAdmin)
        {
            // Employees in administered countries (by their person country code)
            var personCountries = await _context.Employees
                .Select(e => new { e.Id, Scope = e.Person.CountryCode })
                .ToListAsync(cancellationToken);
            foreach (var row in personCountries.Where(r => actor.AdministeredCountryScopeIds.Contains(CountryScopeMapper.ToScopeId(r.Scope))))
            {
                visible.Add(row.Id);
            }

            // Employees created by this account (legacy manažerská vazba)
            var created = await _context.Employees
                .Where(e => e.CreatedByEmployeeId == actor.EmployeeId)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken);
            foreach (var id in created)
            {
                visible.Add(id);
            }
        }

        if (actor.IsCenterAdmin)
        {
            // Employees assigned to any center the actor is assigned to
            var sharedHospital = await _context.HospitalEmployees
                .Where(he => actor.HospitalIds.Contains(he.HospitalId))
                .Select(he => he.EmployeeId)
                .ToListAsync(cancellationToken);
            foreach (var id in sharedHospital)
            {
                visible.Add(id);
            }
        }

        if (actor.EmployeeId != null)
        {
            visible.Add(actor.EmployeeId.Value); // always sees self
        }

        return visible;
    }

    public async Task<string?> ValidateAssignableRolesAsync(IReadOnlyList<int> roleIds, CancellationToken cancellationToken = default)
    {
        var actor = await GetActorScopeAsync(cancellationToken);
        if (!actor.IsScopedAdmin || roleIds.Count == 0)
        {
            return null;
        }

        var roleNames = await _context.Roles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.NameTranslation != null ? r.NameTranslation.EN : "")
            .ToListAsync(cancellationToken);

        var allowed = actor.AssignableRoleNames;
        var rejected = roleNames.Where(name => !allowed.Contains(name)).ToList();
        return rejected.Count == 0
            ? null
            : $"Nemáte oprávnění udělovat tyto role: {string.Join(", ", rejected)}";
    }

    public async Task<string?> ValidateCountryAssignmentAsync(string? countryCode, CancellationToken cancellationToken = default)
    {
        var actor = await GetActorScopeAsync(cancellationToken);
        if (!actor.IsScopedAdmin || string.IsNullOrWhiteSpace(countryCode))
        {
            return null;
        }

        var scopeId = CountryScopeMapper.ToScopeId(countryCode);
        if (actor.IsCenterAdmin && !actor.IsCountryAdmin)
        {
            // Center Admin nesmí zakládat/zařazovat nikoho mimo svou zemi
            var ownScope = actor.AdministeredCountryScopeIds;
            return ownScope.Contains(scopeId)
                ? null
                : "Zaměstnance lze zařadit pouze do země vašeho centra";
        }

        return actor.AdministeredCountryScopeIds.Contains(scopeId)
            ? null
            : "Zaměstnance lze zařadit pouze do zemí, které spravujete";
    }

    public async Task<string?> ValidateHospitalAssignmentAsync(IReadOnlyList<int> hospitalIds, CancellationToken cancellationToken = default)
    {
        if (hospitalIds.Count == 0)
        {
            return null;
        }

        var actor = await GetActorScopeAsync(cancellationToken);
        if (!actor.IsScopedAdmin)
        {
            return null;
        }

        if (actor.IsCenterAdmin && !actor.IsCountryAdmin)
        {
            var outside = hospitalIds.Where(id => !actor.HospitalIds.Contains(id)).ToList();
            return outside.Count == 0
                ? null
                : "Do přiřazení lze zvolit pouze centra, ve kterých působíte";
        }

        // Country Admin: centra ve spravovaných zemích
        var hospitalScopes = await _context.Hospitals
            .Where(h => hospitalIds.Contains(h.Id))
            .Select(h => h.CountryScopeId)
            .ToListAsync(cancellationToken);
        var outsideCountries = hospitalScopes.Where(s => !actor.AdministeredCountryScopeIds.Contains(s)).ToList();
        return outsideCountries.Count == 0
            ? null
            : "Do přiřazení lze zvolit pouze centra v zemích, které spravujete";
    }
}
