namespace DatabaseAPI.APIModels;

public class VisitListItemDto
{
    public int Id { get; set; }
    public DateTime VisitDate { get; set; }
    public string? Note { get; set; }
    public int DocumentCount { get; set; }
    public bool HasIntakeForm { get; set; }
    public int? SourceEventId { get; set; }
}

public class VisitDocumentDto
{
    public int DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public long FileSize { get; set; }
}

public class VisitEvaluationSummaryDto
{
    public int CaseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Side { get; set; }
    public string? Posture { get; set; }
    public int ReviewCount { get; set; }
    public DateTime? LastEvaluatedAtUtc { get; set; }
}

public class VisitDetailDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public DateTime VisitDate { get; set; }
    public string? Note { get; set; }
    public int? SourceEventId { get; set; }
    public List<VisitDocumentDto> Documents { get; set; } = new();

    /// <summary>Evaluated TDB measurements uploaded to this visit (interactive reports).</summary>
    public List<VisitEvaluationSummaryDto> Evaluations { get; set; } = new();

    /// <summary>Filled when the visit links an intake-form event (viewable form data).</summary>
    public object? IntakeForm { get; set; }
}

public class CreateVisitRequest
{
    public DateTime VisitDate { get; set; }
    public string? Note { get; set; }
}

public class UpdateVisitRequest
{
    public DateTime? VisitDate { get; set; }
    public string? Note { get; set; }

    /// <summary>Link/unlink the intake-form event of the visit.</summary>
    public int? SourceEventId { get; set; }
    public bool ClearSourceEvent { get; set; }
}
