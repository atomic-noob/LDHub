using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models
{
    [Table("faqs")]
    public class Faq
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("question")]
        [MaxLength(500)]
        [Required]
        public string Question { get; set; } = string.Empty;

        [Column("answer")]
        [Required]
        public string Answer { get; set; } = string.Empty;

        [Column("image_path")]
        [MaxLength(500)]
        public string? ImagePath { get; set; }

        [Column("display_order")]
        public int DisplayOrder { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
