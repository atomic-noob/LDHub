namespace LDHub.Models;

public class QuizAttempt
{
    public int Id { get; set; }

    public int UserId   { get; set; }
    public User User    { get; set; } = null!;

    public int ModuleId { get; set; }
    public TrainingModule Module { get; set; } = null!;

    public int QuizId   { get; set; }
    public Quiz Quiz    { get; set; } = null!;

    public int TrainingId { get; set; }
    public Training Training { get; set; } = null!;

    public int CorrectCount   { get; set; }
    public int TotalQuestions { get; set; }
    public int ScorePercentage { get; set; }
    public bool IsPassed { get; set; }

    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
}