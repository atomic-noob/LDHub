using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models
{
    [Table("inquiries")]
    public class Inquiry
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        [MaxLength(255)]
        [Required]
        public string Name { get; set; } = string.Empty;

        [Column("email")]
        [MaxLength(255)]
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Column("message")]
        [Required]
        public string Message { get; set; } = string.Empty;

        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        [Column("submitted_at")]
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}
