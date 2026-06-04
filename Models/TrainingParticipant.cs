using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Models;

[Table("training_participants")]
[PrimaryKey("TrainingId", "UserId")]
public class TrainingParticipant
{
    [Column("training_id")]
    public int TrainingId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("enrollment_date")]
    public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;

    [Column("completion_status")]
    public string CompletionStatus { get; set; } = "Enrolled";

    [ForeignKey("TrainingId")]
    public Training? Training { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }
}