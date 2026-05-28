using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models;

[Table("training_instructors")]
public class TrainingInstructor
{
    [Column("training_id")]
    public int TrainingId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("assigned_date")]
    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

    [ForeignKey("TrainingId")]
public Training? Training { get; set; }

[ForeignKey("UserId")]
public User? User { get; set; }
}