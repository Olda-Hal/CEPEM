namespace DatabaseAPI.APIModels;

public class ExaminationSearchFilters
{
    public string? Language { get; set; } = "cs";
    public string? Search { get; set; }
    public int? HospitalId { get; set; }
    public int? HospitalCountryScopeId { get; set; }
    public string? PatientCountryCode { get; set; }
    public string? ExaminationCountryCode { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 0;
    public int Limit { get; set; } = 50;
}

public class ExaminationSearchResponse
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public List<ExaminationListItemDto> Items { get; set; } = [];
}

public class ExaminationListItemDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public DateTime HappenedAt { get; set; }
    public DateTime? HappenedTo { get; set; }
    public int ExaminationTypeId { get; set; }
    public string ExaminationTypeName { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public int PersonId { get; set; }
    public string PatientFirstName { get; set; } = string.Empty;
    public string PatientLastName { get; set; } = string.Empty;
    public string PatientCountryCode { get; set; } = string.Empty;
    public string ExaminationCountryCode { get; set; } = string.Empty;
    public int? HospitalId { get; set; }
    public string? HospitalName { get; set; }
    public int? HospitalCountryScopeId { get; set; }
    public int DocumentCount { get; set; }
    public List<ExaminationDocumentItemDto> Documents { get; set; } = [];
}

public class ExaminationDocumentItemDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class ExaminationExportResponse
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
}

public class ExaminationExportMetadata
{
    public DateTime GeneratedAtUtc { get; set; }
    public ExaminationSearchFilters Filters { get; set; } = new();
    public int TotalExaminations { get; set; }
    public int TotalFiles { get; set; }
    public List<ExaminationExportItemMetadata> Examinations { get; set; } = [];
}

public class ExaminationExportItemMetadata
{
    public int ExaminationId { get; set; }
    public int EventId { get; set; }
    public DateTime HappenedAt { get; set; }
    public string ExaminationTypeName { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCountryCode { get; set; } = string.Empty;
    public string ExaminationCountryCode { get; set; } = string.Empty;
    public int? HospitalId { get; set; }
    public string? HospitalName { get; set; }
    public int? HospitalCountryScopeId { get; set; }
    public List<ExaminationExportFileMetadata> Files { get; set; } = [];
}

public class ExaminationExportFileMetadata
{
    public int DocumentId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ZipPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
}
