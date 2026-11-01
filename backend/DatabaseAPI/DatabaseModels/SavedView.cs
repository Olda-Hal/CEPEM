using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

/// <summary>
/// A per-employee saved filter/view for a list page. Filters are stored as a
/// small JSON object so each surface can evolve independently.
/// </summary>
public class SavedView
{
    public int Id { get; set; }

    [Required]
    public int EmployeeId { get; set; }

    /// <summary>patients / reservations / employees</summary>
    [Required]
    [StringLength(30)]
    public string Surface { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(4000)]
    public string FiltersJson { get; set; } = "{}";

    public bool IsDefault { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
