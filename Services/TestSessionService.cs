using LDHub.Data;
using LDHub.Helpers;
using LDHub.Models;
using LDHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Services;

public class TestSessionService : ITestSessionService
{
    private readonly AppDbContext _db;

    public TestSessionService(AppDbContext db)
    {
        _db = db;
    }

    public const int GraceSeconds = 30;

    public static bool IsAnswerKey(string key) =>
        key.StartsWith("answer_") && int.TryParse(key.AsSpan(7), out _);

    public static Dictionary<string, string> ReadAnswers(TestSession session)
    {
        if (string.IsNullOrWhiteSpace(session.AnswersJson)) return new Dictionary<string, string>();
        try
        {
            return System.Text.Json.JsonSerializer
                       .Deserialize<Dictionary<string, string>>(session.AnswersJson)
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>Returns the participant's in-progress session, or starts a new one.</summary>
    public async Task<TestSession> StartOrResumeAsync(
        int userId, int trainingId, string testType, int attemptNumber, int timerMinutes)
    {
        var session = await _db.TestSessions.FirstOrDefaultAsync(s =>
            s.UserId == userId && s.TrainingId == trainingId &&
            s.TestType == testType && s.ModuleId == null && s.Status == "InProgress");
        if (session != null) return session;

        var now = DateTime.UtcNow;
        session = new TestSession
        {
            UserId        = userId,
            TrainingId    = trainingId,
            TestType      = testType,
            AttemptNumber = attemptNumber,
            StartedAt     = now,
            ExpiresAt     = timerMinutes > 0 ? now.AddMinutes(timerMinutes) : null,
            Status        = "InProgress"
        };
        _db.TestSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    /// <summary>Grades the answers, records the attempt, and closes the session.</summary>
    public async Task<ParticipantTestAttempt> RecordAttemptAsync(
        TestSession session, Dictionary<string, string> answers, bool auto)
    {
        var training  = await _db.Trainings.FindAsync(session.TrainingId);
        var questions = await _db.Questions
            .Where(q => q.TrainingId == session.TrainingId)
            .ToListAsync();

        decimal score       = 0;
        decimal totalPoints = questions.Sum(q => q.Points);

        foreach (var q in questions)
        {
            answers.TryGetValue($"answer_{q.Id}", out var raw);
            var answer = (raw ?? string.Empty).Trim();
            if (answer.Length == 0) continue;

            bool correct = q.QuestionType is "true_false" or "multiple_choice" or "identification"
                           && answer.Equals(q.CorrectAnswer, StringComparison.OrdinalIgnoreCase);
            if (correct) score += q.Points;
        }

        bool isPassed = session.TestType == TestTypes.PostTest
                        && training != null
                        && score >= training.MinimumScore;   // pre-test never gates

        var attempt = new ParticipantTestAttempt
        {
            TrainingId    = session.TrainingId,
            UserId        = session.UserId,
            TestType      = session.TestType,
            Score         = score,
            TotalPoints   = totalPoints,
            IsPassed      = isPassed,
            AttemptNumber = session.AttemptNumber,
            TakenAt       = DateTime.UtcNow
        };
        _db.ParticipantTestAttempts.Add(attempt);

        if (session.TestType == TestTypes.PostTest)
        {
            var enrollment = await _db.TrainingParticipants
                .FirstOrDefaultAsync(tp => tp.TrainingId == session.TrainingId
                                        && tp.UserId     == session.UserId);
            if (enrollment != null)
            {
                if (isPassed) enrollment.CompletionStatus = "Completed";
                if (session.AttemptNumber == 1 && enrollment.CompletionStatus == "Enrolled")
                    enrollment.CompletionStatus = "In Progress";
            }
        }

        session.AnswersJson = System.Text.Json.JsonSerializer.Serialize(answers);
        session.LastSavedAt = DateTime.UtcNow;
        session.Status      = auto ? "AutoSubmitted" : "Submitted";

        await _db.SaveChangesAsync();
        return attempt;
    }

    /// <summary>Grades saved answers for post-tests whose deadline has passed (participant never came back).</summary>
    public async Task FinalizeExpiredAsync(int userId)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-GraceSeconds);
        var expired = await _db.TestSessions
            .Where(s => s.UserId == userId && s.Status == "InProgress"
                     && s.TestType == TestTypes.PostTest
                     && s.ExpiresAt != null && s.ExpiresAt < cutoff)
            .ToListAsync();

        foreach (var s in expired)
            await RecordAttemptAsync(s, ReadAnswers(s), auto: true);
    }
}
