using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models
{
    // Maps to the 'users' table in MySQL
    // This is the unified table for ALL roles — Admin, CM, RAAM, ResourcePerson, Participant
    [Table("users")]
    public class User
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        // ── Personal Details ──────────────────────────────────────

        [Required]
        [Column("first_name")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Column("middle_name")]
        [MaxLength(100)]
        public string? MiddleName { get; set; }  // Optional — ? means nullable

        [Required]
        [Column("last_name")]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Column("email")]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;  // Must be unique — enforced in AppDbContext

        [Required]
        [Column("password")]
        public string Password { get; set; } = string.Empty;  // Always stored as BCrypt hash

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
        public string? Photo { get; set; }  // Stored filename e.g. "trainee_abc123.jpg"

        // ── Account Status ────────────────────────────────────────

        // "Active" = can log in | "Inactive" = blocked
        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Active";

        // ── Location (Foreign Keys) ───────────────────────────────

        [Column("region_id")]
        public int? RegionId { get; set; }

        [Column("office_id")]
        public int? OfficeId { get; set; }

        [Column("division_id")]
        public int? DivisionId { get; set; }

        // ── Timestamps ────────────────────────────────────────────

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation Properties ─────────────────────────────────
        // These allow EF Core to do JOINs automatically (like LEFT JOIN in PHP)

        [ForeignKey("RegionId")]
        public virtual Region? Region { get; set; }

        [ForeignKey("OfficeId")] 
        public virtual Office? Office { get; set; }

        [ForeignKey("DivisionId")]
        public virtual Division? Division { get; set; }

        // One user can have MANY roles (e.g. Admin + RAAM)
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        // One user can be assigned MANY trainings
        public virtual ICollection<TrainingAssignment> TrainingAssignments { get; set; } = new List<TrainingAssignment>();
    }
}