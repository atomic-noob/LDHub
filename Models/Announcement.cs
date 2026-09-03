using System.ComponentModel.DataAnnotations;

namespace LDHub.Models;

public class Announcement
{
    public int AnnouncementId { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Announcement content is required.")]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    public bool IsActive { get; set; } = true;
}