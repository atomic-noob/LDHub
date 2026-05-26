using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models
{
    [Table("trainings")]
    public class Training
    {
        [Key]
        [Column("training_id")]
        public int TrainingId { get; set; }

        [Required]
        [Column("training_code")]
        [MaxLength(100)]
        public string TrainingCode { get; set; } = string.Empty;

        [Required]
        [Column("training_title")]
        [MaxLength(255)]
        public string TrainingTitle { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("location")]
        [MaxLength(255)]
        public string? Location { get; set; }

        [Column("start_date")]
        public DateTime? StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("training_hrs")]
        public int? TrainingHrs { get; set; }

        [Column("development_act")]
        public string? DevelopmentAct { get; set; }

        [Column("MOVs")]
        public string? MOVs { get; set; }

        [Column("success_indicator")]
        public string? SuccessIndicator { get; set; }

        [Column("proficiency_id")]
        public int? ProficiencyId { get; set; }

        [Column("region_id")]
        public int? RegionId { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}