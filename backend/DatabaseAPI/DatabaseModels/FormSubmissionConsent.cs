namespace DatabaseAPI.DatabaseModels;

public class FormSubmissionConsent
{
    public int Id { get; set; }

    public int FormSubmissionId { get; set; }
    public FormSubmission FormSubmission { get; set; } = null!;

    public bool ConfirmAccuracy { get; set; }
    public bool TermsAccepted { get; set; }

    public string? SignaturePlace { get; set; }
    public DateTime? SignatureDate { get; set; }

    public string? SignatureVector { get; set; }
}
