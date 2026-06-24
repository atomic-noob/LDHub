namespace LDHub.Models;

public class ParticipantTestAttempt
{
    public int Id { get; set; }

    public int TrainingId { get; set; }
    public Training Training { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    // "PreTest" or "PostTest"
    public string TestType { get; set; } = string.Empty;

    public decimal Score { get; set; }
    public decimal TotalPoints { get; set; }
    public bool IsPassed { get; set; }

    // 1, 2, or 3 (PostTest only; PreTest is always 1)
    public int AttemptNumber { get; set; } = 1;

    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
}