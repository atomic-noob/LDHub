using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models;

[Table("module_text_content")]
public class ModuleTextContent
{
    [Key]
    [Column("content_id")]
    public int ContentId { get; set; }

    [Column("module_id")]
    public int ModuleId { get; set; }

    [Column("introduction")]
    public string? Introduction { get; set; }

    [Column("references_text")]
    public string? ReferencesText { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("ModuleId")]
    public virtual TrainingModule? Module { get; set; }

    public virtual ICollection<ModuleTextTopic> Topics { get; set; } = new List<ModuleTextTopic>();
}

[Table("module_text_topics")]
public class ModuleTextTopic
{
    [Key]
    [Column("topic_id")]
    public int TopicId { get; set; }

    [Column("content_id")]
    public int ContentId { get; set; }

    [Required]
    [Column("topic_title")]
    [MaxLength(255)]
    public string TopicTitle { get; set; } = string.Empty;

    [Column("topic_body")]
    public string? TopicBody { get; set; }

    [Column("image_path")]
    [MaxLength(500)]
    public string? ImagePath { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    [ForeignKey("ContentId")]
    public virtual ModuleTextContent? Content { get; set; }
}