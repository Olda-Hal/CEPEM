namespace DatabaseAPI.APIModels;

public class CommunicationTemplateDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public int ScopeId { get; set; }
    public string ScopeName { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class SaveCommunicationTemplateRequest
{
    public string Code { get; set; } = string.Empty;
    public string Channel { get; set; } = "Email";
    public string Language { get; set; } = "cs";
    public string ScopeType { get; set; } = "Global";
    public int ScopeId { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class SmsLogDto
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string ToNumber { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? RecipientName { get; set; }
    public int? ReservationSlotId { get; set; }
}
