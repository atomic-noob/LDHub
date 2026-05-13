using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models
{
    // Maps to the 'user_roles' table
    // One user can have MULTIPLE roles e.g. Admin + RAAM
    // This is the junction table that links users to their roles
    [Table("user_roles")]
    public class UserRole
    {
        [Key]
        [Column("user_role_id")]
        public int UserRoleId { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        // Allowed values: "Admin" | "CourseManager" | "RAAM" | "ResourcePerson" | "Participant"
        [Required]
        [Column("role")]
        [MaxLength(50)]
        public string Role { get; set; } = string.Empty;

        [Column("assigned_at")]
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        // Navigation — links back to the User
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}