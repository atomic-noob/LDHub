using LDHub.Data;
using LDHub.Helpers;
using LDHub.Models;
using LDHub.Services;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Tests;

public class TestSessionServiceTests
{
    // Training 1 has three questions worth 1 point each; passing score is 2.
    private static AppDbContext SeedTraining()
    {
        var db = TestDb.Create();
        db.Users.Add(TestDb.NewUser(5));
        db.Trainings.Add(TestDb.NewTraining(1, minimumScore: 2));
        db.Questions.Add(TestDb.NewQuestion(1, 1, "true_false", "True"));
        db.Questions.Add(TestDb.NewQuestion(2, 1, "multiple_choice", "B"));
        db.Questions.Add(TestDb.NewQuestion(3, 1, "identification", "photosynthesis"));
        db.TrainingParticipants.Add(new TrainingParticipant { TrainingId = 1, UserId = 5, CompletionStatus = EnrollmentStatus.Enrolled });
        db.SaveChanges();
        return db;
    }

    private static Dictionary<string, string> All(string a1, string a2, string a3) =>
        new() { ["answer_1"] = a1, ["answer_2"] = a2, ["answer_3"] = a3 };

    // ── start / resume ───────────────────────────────────────────
    [Fact]
    public async Task Starting_a_timed_test_sets_a_server_side_deadline()
    {
        using var db = SeedTraining();
        var service = new TestSessionService(db);

        var session = await service.StartOrResumeAsync(5, 1, TestTypes.PostTest, 1, timerMinutes: 30);

        Assert.Equal("InProgress", session.Status);
        Assert.NotNull(session.ExpiresAt);
        var minutes = (session.ExpiresAt!.Value - session.StartedAt).TotalMinutes;
        Assert.InRange(minutes, 29.9, 30.1);
    }

    [Fact]
    public async Task A_test_with_no_timer_has_no_deadline()
    {
        using var db = SeedTraining();
        var service = new TestSessionService(db);

        var session = await service.StartOrResumeAsync(5, 1, TestTypes.PreTest, 1, timerMinutes: 0);

        Assert.Null(session.ExpiresAt);
    }

    [Fact]
    public async Task Opening_the_test_again_resumes_the_same_session()
    {
        using var db = SeedTraining();
        var service = new TestSessionService(db);

        var first  = await service.StartOrResumeAsync(5, 1, TestTypes.PostTest, 1, 30);
        var second = await service.StartOrResumeAsync(5, 1, TestTypes.PostTest, 2, 30);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, second.AttemptNumber);                 // the server keeps the original attempt number
        Assert.Equal(1, await db.TestSessions.CountAsync());
    }

    // ── grading ──────────────────────────────────────────────────
    [Fact]
    public async Task All_correct_answers_pass_and_complete_the_enrollment()
    {
        using var db = SeedTraining();
        var service = new TestSessionService(db);
        var session = await service.StartOrResumeAsync(5, 1, TestTypes.PostTest, 1, 30);

        var attempt = await service.RecordAttemptAsync(session, All("True", "B", "PHOTOSYNTHESIS"), auto: false);

        Assert.Equal(3m, attempt.Score);
        Assert.Equal(3m, attempt.TotalPoints);
        Assert.True(attempt.IsPassed);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal("Submitted", session.Status);
        Assert.Equal(EnrollmentStatus.Completed,
            (await db.TrainingParticipants.SingleAsync()).CompletionStatus);
    }

    [Fact]
    public async Task Answers_are_not_case_sensitive()
    {
        using var db = SeedTraining();
        var service = new TestSessionService(db);
        var session = await service.StartOrResumeAsync(5, 1, TestTypes.PostTest, 1, 30);

        var attempt = await service.RecordAttemptAsync(session, All("true", "b", "Photosynthesis"), auto: false);

        Assert.Equal(3m, attempt.Score);
    }

    [Fact]
    public async Task A_failing_first_attempt_moves_the_enrollment_to_in_progress()
    {
        using var db = SeedTraining();
        var service = new TestSessionService(db);
        var session = await service.StartOrResumeAsync(5, 1, TestTypes.PostTest, 1, 30);

        var attempt = await service.RecordAttemptAsync(
            session, new Dictionary<string, string> { ["answer_1"] = "True" }, auto: false);

        Assert.Equal(1m, attempt.Score);
        Assert.False(attempt.IsPassed);
        Assert.Equal(EnrollmentStatus.InProgress,
            (await db.TrainingParticipants.SingleAsync()).CompletionStatus);
    }

    [Fact]
    public async Task Blank_and_missing_answers_score_nothing()
    {
        using var db = SeedTraining();
        var service = new TestSessionService(db);
        var session = await service.StartOrResumeAsync(5, 1, TestTypes.PostTest, 1, 30);

        var attempt = await service.RecordAttemptAsync(session, All("", "  ", ""), auto: false);

        Assert.Equal(0m, attempt.Score);
    }

    [Fact]
    public async Task A_pre_test_never_passes_and_never_changes_the_enrollment()
    {
        using var db = SeedTraining();
        var service = new TestSessionService(db);
        var session = await service.StartOrResumeAsync(5, 1, TestTypes.PreTest, 1, 0);

        var attempt = await service.RecordAttemptAsync(session, All("True", "B", "photosynthesis"), auto: false);

        Assert.Equal(3m, attempt.Score);
        Assert.False(attempt.IsPassed);
        Assert.Equal(EnrollmentStatus.Enrolled,
            (await db.TrainingParticipants.SingleAsync()).CompletionStatus);
    }

    [Fact]
    public async Task An_auto_submitted_test_is_marked_that_way()
    {
        using var db = SeedTraining();
        var service = new TestSessionService(db);
        var session = await service.StartOrResumeAsync(5, 1, TestTypes.PostTest, 1, 30);

        await service.RecordAttemptAsync(session, All("True", "", ""), auto: true);

        Assert.Equal("AutoSubmitted", session.Status);
    }

    // ── abandoned tests ──────────────────────────────────────────
    private static TestSession Abandoned(DateTime expiresAt, string type = TestTypes.PostTest) => new()
    {
        UserId = 5, TrainingId = 1, TestType = type, AttemptNumber = 1,
        ExpiresAt = expiresAt, Status = "InProgress",
        AnswersJson = "{\"answer_1\":\"True\",\"answer_2\":\"B\"}"
    };

    [Fact]
    public async Task A_post_test_whose_time_ran_out_is_graded_from_the_saved_answers()
    {
        using var db = SeedTraining();
        db.TestSessions.Add(Abandoned(DateTime.UtcNow.AddMinutes(-5)));
        await db.SaveChangesAsync();

        await new TestSessionService(db).FinalizeExpiredAsync(5);

        var attempt = await db.ParticipantTestAttempts.SingleAsync();
        Assert.Equal(2m, attempt.Score);
        Assert.True(attempt.IsPassed);                           // 2 of 3 meets the passing score
        Assert.Equal("AutoSubmitted", (await db.TestSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_test_that_is_still_running_is_left_alone()
    {
        using var db = SeedTraining();
        db.TestSessions.Add(Abandoned(DateTime.UtcNow.AddMinutes(10)));
        await db.SaveChangesAsync();

        await new TestSessionService(db).FinalizeExpiredAsync(5);

        Assert.Empty(db.ParticipantTestAttempts);
        Assert.Equal("InProgress", (await db.TestSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_test_just_past_its_deadline_gets_a_short_grace_period()
    {
        using var db = SeedTraining();
        db.TestSessions.Add(Abandoned(DateTime.UtcNow.AddSeconds(-(TestSessionService.GraceSeconds - 10))));
        await db.SaveChangesAsync();

        await new TestSessionService(db).FinalizeExpiredAsync(5);

        Assert.Empty(db.ParticipantTestAttempts);
    }

    [Fact]
    public async Task An_unfinished_pre_test_is_never_auto_graded()
    {
        using var db = SeedTraining();
        db.TestSessions.Add(Abandoned(DateTime.UtcNow.AddMinutes(-5), TestTypes.PreTest));
        await db.SaveChangesAsync();

        await new TestSessionService(db).FinalizeExpiredAsync(5);

        Assert.Empty(db.ParticipantTestAttempts);
    }

    [Fact]
    public async Task Only_the_given_users_sessions_are_finalized()
    {
        using var db = SeedTraining();
        db.Users.Add(TestDb.NewUser(6));
        var other = Abandoned(DateTime.UtcNow.AddMinutes(-5));
        other.UserId = 6;
        db.TestSessions.Add(other);
        await db.SaveChangesAsync();

        await new TestSessionService(db).FinalizeExpiredAsync(5);

        Assert.Empty(db.ParticipantTestAttempts);
    }

    // ── small helpers ────────────────────────────────────────────
    [Theory]
    [InlineData("answer_12", true)]
    [InlineData("answer_1", true)]
    [InlineData("answer_", false)]
    [InlineData("answer_x", false)]
    [InlineData("other_5", false)]
    public void Only_answer_fields_are_accepted(string key, bool expected)
    {
        Assert.Equal(expected, TestSessionService.IsAnswerKey(key));
    }

    [Fact]
    public void Saved_answers_are_read_back_and_bad_data_gives_an_empty_set()
    {
        var good = TestSessionService.ReadAnswers(new TestSession { AnswersJson = "{\"answer_1\":\"B\"}" });
        var bad  = TestSessionService.ReadAnswers(new TestSession { AnswersJson = "not json" });
        var none = TestSessionService.ReadAnswers(new TestSession());

        Assert.Equal("B", good["answer_1"]);
        Assert.Empty(bad);
        Assert.Empty(none);
    }
}
