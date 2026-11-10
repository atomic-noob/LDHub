using LDHub.Models;

namespace LDHub.Services.Interfaces;

/// <summary>Pre-test, post-test and quiz sessions: start, resume, grade, and finish.</summary>
public interface ITestSessionService
{
    /// <summary>Returns the participant's in-progress session, or starts a new one.</summary>
    Task<TestSession> StartOrResumeAsync(int userId, int trainingId, string testType, int attemptNumber, int timerMinutes);

    /// <summary>Grades the answers, records the attempt, and closes the session.</summary>
    Task<ParticipantTestAttempt> RecordAttemptAsync(TestSession session, Dictionary<string, string> answers, bool auto);

    /// <summary>Grades saved answers for post-tests whose deadline has passed.</summary>
    Task FinalizeExpiredAsync(int userId);
}
