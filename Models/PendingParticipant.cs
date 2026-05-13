using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models
{
    // Maps to the 'pending_participants' table
    // When an employee signs up, their data goes HERE first
    // Admin reviews → if approved → copied to 'users' table + role assigned
    // If rejected → deleted from here and email sent
    [Table("pending_participants")]
    public class PendingParticipant
    {
        [Key]
        [Column("participant_id")]
        public int ParticipantId { get; set; }

        // ── Personal Details ──────────────────────────────────────

        [Required]
        [Column("first_name")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Column("middle_name")]
        [MaxLength(100)]
        public string? MiddleName { get; set; }

        [Required]
        [Column("last_name")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Column("email")]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("password")]
        public string Password { get; set; } = string.Empty;  // Already BCrypt hashed on save

        [Column("contact_number")]
        [MaxLength(20)]
        public string? ContactNumber { get; set; }

        [Required]
        [Column("gender")]
        [MaxLength(20)]
        public string Gender { get; set; } = string.Empty;

        [Required]
        [Column("age")]
        public int Age { get; set; }

        // ── Work Details ──────────────────────────────────────────

        [Required]
        [Column("position")]
        [MaxLength(255)]
        public string Position { get; set; } = string.Empty;

        [Column("designation")]
        [MaxLength(255)]
        public string? Designation { get; set; }

        [Column("salary_grade")]
        public int? SalaryGrade { get; set; }

        [Column("photo")]
        [MaxLength(255)]
        public string? Photo { get; set; }

        // Always "Inactive" while waiting for admin approval
        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Inactive";

        [Column("in_training")]
        public int InTraining { get; set; } = 0;

        // ── Location (Foreign Keys) ───────────────────────────────

        [Column("region_id")]
        public int? RegionId { get; set; }

        [Column("office_id")]
        public int? OfficeId { get; set; }

        [Column("division_id")]
        public int? DivisionId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("RegionId")]
        public virtual Region? Region { get; set; }

        [ForeignKey("OfficeId")]
        public virtual Office? Office { get; set; }

        [ForeignKey("DivisionId")]
        public virtual Division? Division { get; set; }
    }
}