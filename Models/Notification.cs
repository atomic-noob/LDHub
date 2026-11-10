using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models;

/// <summary>An in-app notification shown in the navbar bell and the Notifications inbox.</summary>
[Table("notifications")]
public class Notification
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("title")]
    [MaxLength(200)]
    [Required]
    public string Title { get; set; } = string.Empty;

    [Column("message")]
    [MaxLength(500)]
    public string? Message { get; set; }

    /// <summary>Local link opened when the notification is clicked (optional).</summary>
    [Column("url")]
    [MaxLength(300)]
    public string? Url { get; set; }

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
