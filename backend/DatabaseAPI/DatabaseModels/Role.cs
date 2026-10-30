namespace DatabaseAPI.DatabaseModels;

public class Role
{
    public int Id { get; set; }

    public int? NameTranslationId { get; set; }
    public Translation? NameTranslation { get; set; }

    /// <summary>
    /// Frontend path the user is redirected to after login when this is their
    /// earliest role with a landing page configured. Null = app default.
    /// </summary>
    public string? DefaultPath { get; set; }

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
