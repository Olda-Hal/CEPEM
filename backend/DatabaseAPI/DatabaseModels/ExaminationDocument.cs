using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DatabaseAPI.DatabaseModels;

public class ExaminationDocument
{
    public int Id { get; set; }

    [Required]
    public int ExaminationId { get; set; }

    [ForeignKey(nameof(ExaminationId))]
    [JsonIgnore]
    public Examination Examination { get; set; } = null!;

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
}
