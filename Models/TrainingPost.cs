using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models;

/// <summary>
/// A post on a training's discussion board.
/// Top-level post (ParentId == null) = a discussion thread or an announcement.
/// Post with a ParentId = a reply in that thread.
/// </summary>
[Table("training_posts")]
public class TrainingPost
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("training_id")]
    public int TrainingId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    /// <summary>Null for a top-level post; the thread's post id for a reply.</summary>
    [Column("parent_id")]
    public int? ParentId { get; set; }

    /// <summary>True for a staff announcement to the whole group (pinned, notifies everyone).</summary>
    [Column("is_announcement")]
    public bool IsAnnouncement { get; set; }

    [Column("title")]
    [MaxLength(200)]
    public string? Title { get; set; }

    [Column("body")]
    [MaxLength(2000)]
    [Required]
    public string Body { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("UserId")]
    public User? User { get; set; }
}
