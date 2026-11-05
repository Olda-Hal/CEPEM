namespace DatabaseAPI.APIModels;

/// <summary>One .tdb file attached to the measurement's visit.</summary>
public class MeasurementDocumentDto
{
    public int DocumentId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public long FileSize { get; set; }
    public int? EvaluationCaseId { get; set; }
}

/// <summary>
/// One visit row of the measurements overview: patient, visit date and the
/// .tdb files uploaded to that visit together with their evaluation state.
/// </summary>
public class MeasurementVisitDto
{
    public int VisitId { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientUid { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }

    /// <summary>"evaluated", "partial" or "pending".</summary>
    public string Status { get; set; } = "pending";

    public int EvaluationCaseCount { get; set; }
    public int EvaluatedCaseCount { get; set; }
    public bool IsManuallyEvaluated { get; set; }
    public DateTime? LastEvaluatedAtUtc { get; set; }

    public List<MeasurementDocumentDto> Documents { get; set; } = new();
}

public class MeasurementListDto
{
    public int Total { get; set; }
    public List<MeasurementVisitDto> Items { get; set; } = new();
}
