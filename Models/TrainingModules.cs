using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models;

[Table("training_modules")]
public class TrainingModule
{
    [Key]
    [Column("module_id")]
    public int ModuleId { get; set; }

    [Required]
    [Column("training_id")]
    public int TrainingId { get; set; }

    [Required]
    [Column("module_name")]
    [MaxLength(100)]
    public string ModuleName { get; set; } = string.Empty;

    [Required]
    [Column("module_description")]
    [MaxLength(500)]
    public string ModuleDescription { get; set; } = string.Empty;

    // "Lecture" | "Activity" | "Quiz" | "Text"
    [Required]
    [Column("module_type")]
    [MaxLength(50)]
    public string ModuleType { get; set; } = string.Empty;

    [Column("file_path")]
    [MaxLength(500)]
    public string? FilePath { get; set; }

    [Column("link_url")]
    [MaxLength(500)]
    public string? LinkUrl { get; set; }

    [Column("content")]
    public string? Content { get; set; }

    // "file" | "link" — only for Lecture type
    [Column("lecture_source")]
    [MaxLength(20)]
    public string? LectureSource { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    [Column("uploaded_by")]
    public int UploadedBy { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("TrainingId")]
    public virtual Training? Training { get; set; }

    [ForeignKey("UploadedBy")]
    public virtual User? UploadedByUser { get; set; }

    public virtual Quiz? Quiz { get; set; }
    public virtual ModuleTextContent? TextContent { get; set; }
}