using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models
{
    // ── TRAINING ──────────────────────────────────────────────────
    // Maps to the 'trainings' table
    // Created by Admin, CourseManager, or RAAM
    // Status: "Draft" = not yet visible | "Published" = visible and assignable
    [Table("trainings")]
    public class Training
    {
        [Key]
        [Column("training_id")]
        public int TrainingId { get; set; }

        [Required]
        [Column("title")]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("location")]
        [MaxLength(255)]
        public string? Location { get; set; }

        [Column("start_date")]
        public DateTime? StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        // "Draft" | "Published" | "Archived"
        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Draft";

        // Who created this training
        [Column("created_by")]
        public int CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("CreatedBy")]
        public virtual User? CreatedByUser { get; set; }

        // One training has MANY modules
        public virtual ICollection<TrainingModule> Modules { get; set; } = new List<TrainingModule>();

        // One training can be assigned to MANY participants
        public virtual ICollection<TrainingAssignment> Assignments { get; set; } = new List<TrainingAssignment>();
    }

    // ── TRAINING MODULE ───────────────────────────────────────────
    // Maps to the 'training_modules' table
    // Uploaded by CourseManager, RAAM, or ResourcePerson
    // Types: "Lecture" | "Activity" | "Quiz" | "Text"
    [Table("training_modules")]
    public class TrainingModule
    {
        [Key]
        [Column("module_id")]
        public int ModuleId { get; set; }

        [Required]
        [Column("training_id")]
        public int TrainingId { get; set; }

        // Module group name e.g. "Module 1", "Module 2" ... "Module 30"
        [Required]
        [Column("module_name")]
        [MaxLength(100)]
        public string ModuleName { get; set; } = string.Empty;

        // Short description/title of the module
        [Required]
        [Column("module_description")]
        [MaxLength(500)]
        public string ModuleDescription { get; set; } = string.Empty;

        // "Lecture" | "Activity" | "Quiz" | "Text"
        [Required]
        [Column("module_type")]
        [MaxLength(50)]
        public string ModuleType { get; set; } = string.Empty;

        // For Lecture (file source) — stored filename in uploads/modules/
        [Column("file_path")]
        [MaxLength(500)]
        public string? FilePath { get; set; }

        // For Lecture (link source) or Activity — external URL
        [Column("link_url")]
        [MaxLength(500)]
        public string? LinkUrl { get; set; }

        // For Text type — plain text content stored in DB
        [Column("content")]
        public string? Content { get; set; }

        // "file" | "link" | "text" — only used for Lecture type
        [Column("lecture_source")]
        [MaxLength(20)]
        public string? LectureSource { get; set; }

        // Controls display order in training detail page
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        // Who uploaded this module
        [Column("uploaded_by")]
        public int UploadedBy { get; set; }

        [Column("uploaded_at")]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("TrainingId")]
        public virtual Training? Training { get; set; }

        [ForeignKey("UploadedBy")]
        public virtual User? UploadedByUser { get; set; }
    }

    // ── TRAINING ASSIGNMENT ───────────────────────────────────────
    // Maps to the 'training_assignments' table
    // Created when Admin/CM/RAAM assigns a training to a participant
    // Tracks the participant's progress through the training
    [Table("training_assignments")]
    public class TrainingAssignment
    {
        [Key]
        [Column("assignment_id")]
        public int AssignmentId { get; set; }

        [Required]
        [Column("training_id")]
        public int TrainingId { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        // "Pending" → "Accepted" → "Ongoing" → "Completed" | "Failed"
        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        [Column("assigned_at")]
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        [Column("accepted_at")]
        public DateTime? AcceptedAt { get; set; }  // Set when participant starts training

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }  // Set when post-test is done

        // Navigation
        [ForeignKey("TrainingId")]
        public virtual Training? Training { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}