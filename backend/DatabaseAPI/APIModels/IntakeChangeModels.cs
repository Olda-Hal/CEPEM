namespace DatabaseAPI.APIModels;

/// <summary>Master-data fields proposed by a public intake submission.</summary>
public class MasterDataChangesDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public int? InsuranceNumber { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
}

public class IntakeChangeRequestDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int? IntakeEventId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public MasterDataChangesDto Changes { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public int? ReviewedByEmployeeId { get; set; }
}
