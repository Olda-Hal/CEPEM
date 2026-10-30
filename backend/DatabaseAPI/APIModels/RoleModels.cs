namespace DatabaseAPI.APIModels;

public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? DefaultPath { get; set; }
}

public class UpdateRoleDefaultPathRequest
{
    public string? DefaultPath { get; set; }
}

/// <summary>
/// Frontend routes an admin may pick as a role's landing page. Kept in sync
/// with the protected routes declared in the SPA (frontend/src/App.tsx plus
/// the evaluation page) — anything else would produce a dead landing link.
/// </summary>
public static class RoleDefaultPaths
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "/patients",
        "/reservations",
        "/examinations",
        "/administration",
        "/admin",
        "/admin/activity-log",
        "/settings",
        "/vyhodnocovani"
    };

    public static bool IsAllowed(string? path)
        => path == null || Allowed.Contains(path);
}
