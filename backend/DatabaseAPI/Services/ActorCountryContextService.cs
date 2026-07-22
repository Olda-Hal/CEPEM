using DatabaseAPI.Data;
using DatabaseAPI.Utils;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Services;

public interface IActorCountryContextService
{
    Task<string> GetActorCountryCodeAsync(CancellationToken cancellationToken = default);
    Task<int> GetActorCountryScopeIdAsync(CancellationToken cancellationToken = default);
    int? GetActorEmployeeId();
}

public class ActorCountryContextService : IActorCountryContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly DatabaseContext _context;

    public ActorCountryContextService(IHttpContextAccessor httpContextAccessor, DatabaseContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public async Task<string> GetActorCountryCodeAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var headerCountryCode = httpContext?.Request.Headers["X-Actor-Country-Code"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerCountryCode))
            return CountryScopeMapper.NormalizeCode(headerCountryCode);

        var employeeIdHeader = httpContext?.Request.Headers["X-Actor-Employee-Id"].FirstOrDefault();
        if (int.TryParse(employeeIdHeader, out var employeeId) && employeeId > 0)
        {
            var countryCode = await _context.Employees
                .Where(employee => employee.Id == employeeId)
                .Select(employee => employee.Person.CountryCode)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(countryCode))
                return CountryScopeMapper.NormalizeCode(countryCode);
        }

        return "CZ";
    }

    public async Task<int> GetActorCountryScopeIdAsync(CancellationToken cancellationToken = default)
    {
        var countryCode = await GetActorCountryCodeAsync(cancellationToken);
        return CountryScopeMapper.ToScopeId(countryCode);
    }

    public int? GetActorEmployeeId()
    {
        var employeeIdHeader = _httpContextAccessor.HttpContext?.Request.Headers["X-Actor-Employee-Id"].FirstOrDefault();
        if (int.TryParse(employeeIdHeader, out var employeeId) && employeeId > 0)
            return employeeId;

        return null;
    }
}
