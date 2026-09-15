using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models
{
    [Table("free_trainings")]
    public class FreeTraining
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("title")]
        [MaxLength(255)]
        [Required]
        public string Title { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("location")]
        [MaxLength(255)]
        public string? Location { get; set; }

        [Column("region_id")]
        public int? RegionId { get; set; }

        [Column("start_date")]
        public DateTime? StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("registration_link")]
        [MaxLength(500)]
        public string? RegistrationLink { get; set; }

        [Column("photo")]
        [MaxLength(255)]
        public string? Photo { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // Navigation
        [ForeignKey("RegionId")]
        public virtual Region? Region { get; set; }
    }
}
