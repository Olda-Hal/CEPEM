namespace DatabaseAPI.APIModels;

/// <summary>
/// Aggregated global-search result. The internal DatabaseAPI endpoint returns
/// all requested categories; the gateway decides which categories the caller
/// may see based on their effective permissions.
/// </summary>
public class GlobalSearchResponse
{
    public string Query { get; set; } = string.Empty;
    public List<SearchPatientDto> Patients { get; set; } = new();
    public List<SearchVisitDto> Visits { get; set; } = new();
    public List<SearchDocumentDto> Documents { get; set; } = new();
    public List<SearchEvaluationDto> Evaluations { get; set; } = new();
    public List<SearchEmployeeDto> Employees { get; set; } = new();
}

public class SearchPatientDto
{
    public int Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}

public class SearchVisitDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    public string? Note { get; set; }
    public bool HasIntakeForm { get; set; }
}

public class SearchDocumentDto
{
    public int DocumentId { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

/// <summary>No patient identity is exposed for .tdb evaluation results.</summary>
public class SearchEvaluationDto
{
    public int CaseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Side { get; set; }
    public string? Posture { get; set; }
    public int ReviewCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class SearchEmployeeDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool Active { get; set; }
    public List<string> Roles { get; set; } = new();
}
