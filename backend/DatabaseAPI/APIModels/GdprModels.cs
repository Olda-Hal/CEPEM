namespace DatabaseAPI.APIModels;

public class GdprDataMapDto
{
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public int Visits { get; set; }
    public int Documents { get; set; }
    public int Photos { get; set; }
    public int Events { get; set; }
    public int FormSubmissions { get; set; }
    public int EvaluationCases { get; set; }
    public int Consents { get; set; }
    public int EmailLogs { get; set; }
    public DateTime? LastActivityAtUtc { get; set; }
}

public class AnonymizePatientRequest
{
    /// <summary>Patient UID or the literal ANONYMIZE as an explicit confirmation.</summary>
    public string Confirmation { get; set; } = string.Empty;
}

public class RecordConsentRequest
{
    public string ConsentType { get; set; } = "gdpr";
    public string Version { get; set; } = "1";
    public string Language { get; set; } = "cs";
    public string Source { get; set; } = "Paper";
    public string? Notes { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
}

public class ConsentTemplateDto
{
    public int Id { get; set; }
    public string ConsentType { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class SaveConsentTemplateRequest
{
    public string ConsentType { get; set; } = "gdpr";
    public string Version { get; set; } = "1";
    public string Language { get; set; } = "cs";
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class PatientConsentDto
{
    public int Id { get; set; }
    public string ConsentType { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime AcceptedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? Notes { get; set; }
}
