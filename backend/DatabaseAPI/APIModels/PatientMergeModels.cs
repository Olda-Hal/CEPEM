namespace DatabaseAPI.APIModels;

public class PatientMergeRequest
{
    public int SourcePatientId { get; set; }
    public int TargetPatientId { get; set; }
}

public class PatientMergePreviewDto
{
    public int SourcePatientId { get; set; }
    public int TargetPatientId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string SourceUid { get; set; } = string.Empty;
    public string TargetUid { get; set; } = string.Empty;
    public int Events { get; set; }
    public int Visits { get; set; }
    public int Documents { get; set; }
    public int FormSubmissions { get; set; }
    public int Consents { get; set; }
    public int EvaluationCases { get; set; }
    public int IntakeChanges { get; set; }
    public int GdprRequests { get; set; }
    public int ReservationSlots { get; set; }
    public int Reservations { get; set; }
    public int Appointments { get; set; }
    public int EmailLogs { get; set; }
    public int SmsLogs { get; set; }
    public int IntakeLinks { get; set; }
}

public class PatientMergeResultDto
{
    public int SourcePatientId { get; set; }
    public int TargetPatientId { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public string TargetUid { get; set; } = string.Empty;
    public int MovedEvents { get; set; }
    public int MovedVisits { get; set; }
    public int MovedDocuments { get; set; }
    public int MovedFormSubmissions { get; set; }
    public int MovedConsents { get; set; }
    public int MovedEvaluationCases { get; set; }
    public int MovedIntakeChanges { get; set; }
    public int MovedPersonReferences { get; set; }
}
