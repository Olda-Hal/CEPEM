using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DatabaseAPI.DatabaseModels;

public class PatientDocument
{
  [Key]
  public int Id { get; set; }

  [Required]
  public int PatientId { get; set; }

  [ForeignKey(nameof(PatientId))]
  [JsonIgnore]
  public Patient Patient { get; set; } = null!;

  [Required]
  [MaxLength(255)]
  public string FileName { get; set; } = null!;

  [Required]
  [MaxLength(255)]
  public string OriginalFileName { get; set; } = null!;

  [Required]
  public DateTime UploadedAt { get; set; }

  [Required]
  public long FileSize { get; set; }

  [Required]
  [MaxLength(500)]
  public string EncryptedPath { get; set; } = null!;

  public bool IsDeleted { get; set; } = false;

  /// <summary>Searchable text layer extracted by OCR / PDF text extraction.</summary>
  public string? ExtractedText { get; set; }

  /// <summary>Pending / Extracted / NoText / Unsupported / Unavailable / Failed.</summary>
  [MaxLength(20)]
  public string ExtractionStatus { get; set; } = "Pending";

  [MaxLength(500)]
  public string? ExtractionError { get; set; }

  public DateTime? ExtractedAtUtc { get; set; }
}
