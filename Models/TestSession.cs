using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models;

/// <summary>
/// A test or quiz that a participant has started. It lives in the database (not the web session),
/// so progress survives a closed tab, an expired login, or a server restart.
/// TestType: "PreTest", "PostTest" or "Quiz" (quizzes also set ModuleId).
/// </summary>
[Table("test_sessions")]
public class TestSession
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("training_id")]
    public int TrainingId { get; set; }

    [Column("module_id")]
    public int? ModuleId { get; set; }

    [Column("test_type")]
    [MaxLength(20)]
    [Required]
    public string TestType { get; set; } = string.Empty;

    /// <summary>Decided by the server when the test starts.</summary>
    [Column("attempt_number")]
    public int AttemptNumber { get; set; } = 1;

    [Column("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Server-side deadline (UTC). Null when the test has no time limit.</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Latest autosaved answers: {"answer_12":"B", ...}</summary>
    [Column("answers_json")]
    public string? AnswersJson { get; set; }

    [Column("last_saved_at")]
    public DateTime? LastSavedAt { get; set; }

    /// <summary>InProgress, Submitted, or AutoSubmitted (time ran out and saved answers were graded).</summary>
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "InProgress";
}
