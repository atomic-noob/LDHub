using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;
using LDHub.Models.ViewModels;

namespace LDHub.Controllers;

public class EmployeeController : Controller
{
    private readonly AppDbContext _db;

    public EmployeeController(AppDbContext db)
    {
        _db = db;
    }

    // ── Auth check helper ─────────────────────────────────────────
    private bool IsEmployee() =>
        HttpContext.Session.GetString("UserRole") == "Employee";

    // ── Get current logged in user ID from session ─────────────────
    private int GetUserId()
{
    var str = HttpContext.Session.GetString("UserId");
    return int.TryParse(str, out int id) ? id : 0;
}

    // ── ViewBag helper ────────────────────────────────────────────
    private void SetViewBag()
    {
        ViewBag.ActiveRole = "Employee";
        ViewBag.UserName   = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto  = HttpContext.Session.GetString("UserPhoto");
    }

    // ── GET: /Employee/Dashboard ──────────────────────────────────
    public async Task<IActionResult> Dashboard()
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var userId = GetUserId();
        var now    = DateTime.Now;
        // Grade tests whose time ran out while the participant was away
        await FinalizeExpiredSessionsAsync(userId);

        // Get all enrollments
        var enrollments = await _db.TrainingParticipants
            .Include(tp => tp.Training)
            .Where(tp => tp.UserId == userId)
            .ToListAsync();

        // ── Auto-complete trainings past their end date ──
        bool anyUpdated = false;
        foreach (var tp in enrollments)
        {// Only auto-complete if EndDate is set AND has passed
    if (tp.Training?.EndDate != null
    && now > tp.Training.EndDate
    && tp.CompletionStatus != "Completed")
            {
                tp.CompletionStatus = "Completed";
                anyUpdated = true;
            }
        }
        if (anyUpdated) await _db.SaveChangesAsync();

       // ── Get current active enrollment (include upcoming ones too) ──
                var currentEnrollment = enrollments
                    .Where(tp => tp.CompletionStatus != "Completed"
                            && (tp.Training?.EndDate == null || tp.Training.EndDate >= now))
                    .OrderBy(tp => tp.Training?.StartDate)
                    .FirstOrDefault();

                // ── Flag if training hasn't started yet ──
                var trainingNotStarted = currentEnrollment != null
                    && currentEnrollment.Training?.StartDate != null
                    && currentEnrollment.Training.StartDate > now;

                ViewBag.TrainingNotStarted = trainingNotStarted;

        // ── Get modules for current training ──
        var modules = new List<TrainingModule>();
        if (currentEnrollment != null)
        {
           modules = await _db.TrainingModules
            .Include(m => m.TextContent)
                .ThenInclude(tc => tc.Topics)
            .Where(m => m.TrainingId == currentEnrollment.TrainingId)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();
        }
        // ── Load completed module IDs for this user ──
var completedModuleIds = new List<int>();
if (currentEnrollment != null)
{
    completedModuleIds = await _db.ModuleProgresses
        .Where(mp => mp.UserId     == userId
                  && mp.TrainingId == currentEnrollment.TrainingId)
        .Select(mp => mp.ModuleId)
        .ToListAsync();
}
ViewBag.CompletedModuleIds = completedModuleIds;

        // ── Check if pre-test has been taken ──
        bool preTestTaken = false;
        if (currentEnrollment != null)
        {
            preTestTaken = await _db.ParticipantTestAttempts
                .AnyAsync(a => a.UserId     == userId
                            && a.TrainingId == currentEnrollment.TrainingId
                            && a.TestType   == "PreTest");
        }

        // ── Check if all modules are done and post-test is available ──
        // (post-test unlocks only after pre-test taken — module completion
        //  is tracked client-side for now via checkboxes)
        bool postTestTaken = false;
        int  postTestAttempts = 0;
        bool postTestPassed   = false;
        if (currentEnrollment != null)
        {
            var postAttempts = await _db.ParticipantTestAttempts
                .Where(a => a.UserId     == userId
                         && a.TrainingId == currentEnrollment.TrainingId
                         && a.TestType   == "PostTest")
                .ToListAsync();

            postTestTaken    = postAttempts.Any();
            postTestAttempts = postAttempts.Count;
            postTestPassed   = postAttempts.Any(a => a.IsPassed);
        }
        // ── Check evaluation status ──
bool evalDone = false;
bool hasEvalQuestions = false;
if (currentEnrollment != null)
{
    evalDone = await _db.EvaluationResponses
        .AnyAsync(r => r.UserId == userId 
                    && r.TrainingId == currentEnrollment.TrainingId);

    hasEvalQuestions = await _db.EvaluationQuestions
        .AnyAsync(q => q.TrainingId == currentEnrollment.TrainingId);
}

ViewBag.EvalDone         = evalDone;
ViewBag.HasEvalQuestions = hasEvalQuestions;

        ViewBag.TotalEnrolled   = enrollments.Count;
        ViewBag.TotalCompleted  = enrollments.Count(tp => tp.CompletionStatus == "Completed");
        ViewBag.TotalOngoing    = enrollments.Count(tp =>
            tp.Training != null &&
            now >= tp.Training.StartDate &&
            now <= tp.Training.EndDate);
        ViewBag.Enrollment      = currentEnrollment;
        ViewBag.Modules         = modules;
        ViewBag.CompletedCount  = enrollments.Count(tp => tp.CompletionStatus == "Completed");
        ViewBag.PreTestTaken    = preTestTaken;
        ViewBag.PostTestTaken   = postTestTaken;
        ViewBag.PostTestAttempts = postTestAttempts;
        ViewBag.PostTestPassed  = postTestPassed;
        ViewBag.CompletedModuleIds = await _db.ModuleProgresses
    .Where(mp => mp.UserId == userId)
    .Select(mp => mp.ModuleId)
    .ToListAsync();

        return View("~/Views/Employee/Dashboard.cshtml", enrollments);
    }


    // ══════════════════════════════════════════════════════════════
    //  TEST SESSIONS: progress is kept in the database, not the browser
    // ══════════════════════════════════════════════════════════════
    private const int TestGraceSeconds = 30;

    private static bool IsAnswerKey(string key) =>
        key.StartsWith("answer_") && int.TryParse(key.AsSpan(7), out _);

    private static Dictionary<string, string> ReadAnswers(TestSession session)
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
    private async Task<TestSession> StartOrResumeSessionAsync(
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
    private async Task<ParticipantTestAttempt> RecordAttemptAsync(
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

        bool isPassed = session.TestType == "PostTest"
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

        if (session.TestType == "PostTest")
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
    private async Task FinalizeExpiredSessionsAsync(int userId)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-TestGraceSeconds);
        var expired = await _db.TestSessions
            .Where(s => s.UserId == userId && s.Status == "InProgress"
                     && s.TestType == "PostTest"
                     && s.ExpiresAt != null && s.ExpiresAt < cutoff)
            .ToListAsync();

        foreach (var s in expired)
            await RecordAttemptAsync(s, ReadAnswers(s), auto: true);
    }

    private IActionResult RedirectToResult(ParticipantTestAttempt a) =>
        RedirectToAction("TestResult", new
        {
            trainingId    = a.TrainingId,
            testType      = a.TestType,
            score         = (int)a.Score,
            totalPoints   = (int)a.TotalPoints,
            isPassed      = a.IsPassed ? 1 : 0,
            attemptNumber = a.AttemptNumber
        });

    // ── POST: /Employee/SaveProgress (autosave from the test and quiz pages) ──
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProgress([FromBody] SaveProgressRequest req)
    {
        if (!IsEmployee()) return Json(new { ok = false, loggedOut = true });
        var userId = GetUserId();

        if (req.TestType is not ("PreTest" or "PostTest" or "Quiz"))
            return Json(new { ok = false });

        var session = await _db.TestSessions.FirstOrDefaultAsync(s =>
            s.UserId == userId && s.TrainingId == req.TrainingId &&
            s.TestType == req.TestType && s.ModuleId == req.ModuleId &&
            s.Status == "InProgress");

        if (session == null) return Json(new { ok = false, noSession = true });

        var now = DateTime.UtcNow;
        if (session.ExpiresAt != null && now > session.ExpiresAt.Value.AddSeconds(TestGraceSeconds))
            return Json(new { ok = false, expired = true });

        var clean = (req.Answers ?? new Dictionary<string, string?>())
            .Where(kv => IsAnswerKey(kv.Key) && (kv.Value?.Length ?? 0) <= 500)
            .Take(500)
            .ToDictionary(kv => kv.Key, kv => kv.Value ?? string.Empty);

        session.AnswersJson = System.Text.Json.JsonSerializer.Serialize(clean);
        session.LastSavedAt = now;
        await _db.SaveChangesAsync();

        int? secondsLeft = session.ExpiresAt == null
            ? null
            : Math.Max(0, (int)(session.ExpiresAt.Value - now).TotalSeconds);

        return Json(new { ok = true, secondsLeft });
    }

    // ── POST: /Employee/KeepAlive (stops the login from expiring during a long test) ──
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult KeepAlive() => Json(new { ok = IsEmployee() });

    // ── GET: /Employee/PreTest ────────────────────────────────────
    public async Task<IActionResult> PreTest(int trainingId)
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var userId = GetUserId();

        // Block if already taken
        var alreadyTaken = await _db.ParticipantTestAttempts
            .AnyAsync(a => a.UserId     == userId
                        && a.TrainingId == trainingId
                        && a.TestType   == "PreTest");

        if (alreadyTaken)
        {
            TempData["Error"] = "You have already taken the pre-test for this training.";
            return RedirectToAction("Dashboard");
        }

        // Load training + questions
        var training = await _db.Trainings.FindAsync(trainingId);
        if (training == null) return NotFound();

        var questions = await _db.Questions
            .Where(q => q.TrainingId == trainingId)
            .OrderBy(q => q.Id)
            .ToListAsync();

        ViewBag.Training  = training;
        ViewBag.Questions = questions;
        ViewBag.TestType  = "PreTest";

        var session = await StartOrResumeSessionAsync(userId, trainingId, "PreTest", 1, 0);
        ViewBag.AttemptNumber = 1;
        ViewBag.SavedAnswers  = ReadAnswers(session);

        return View("~/Views/Employee/TakeTest.cshtml");
    }

    // ── GET: /Employee/PostTest ───────────────────────────────────
    public async Task<IActionResult> PostTest(int trainingId)
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var userId = GetUserId();

        // Grade any earlier post-test whose time ran out while the participant was away
        await FinalizeExpiredSessionsAsync(userId);

        // Check attempt count — max 3
        var attempts = await _db.ParticipantTestAttempts
            .Where(a => a.UserId     == userId
                     && a.TrainingId == trainingId
                     && a.TestType   == "PostTest")
            .ToListAsync();

        if (attempts.Count >= 3)
        {
            TempData["Error"] = "You have used all 3 post-test attempts.";
            return RedirectToAction("Dashboard");
        }

        // Must have taken pre-test first
        var preTestTaken = await _db.ParticipantTestAttempts
            .AnyAsync(a => a.UserId     == userId
                        && a.TrainingId == trainingId
                        && a.TestType   == "PreTest");

        if (!preTestTaken)
        {
            TempData["Error"] = "Please complete the pre-test first.";
            return RedirectToAction("Dashboard");
        }
        // Check if evaluation is completed
var evaluationDone = await _db.EvaluationResponses
    .AnyAsync(r => r.UserId == userId && r.TrainingId == trainingId);

var hasQuestions = await _db.EvaluationQuestions
    .AnyAsync(q => q.TrainingId == trainingId);

if (hasQuestions && !evaluationDone)
{
    TempData["Info"] = "Please complete the evaluation first before taking the Post-Test.";
    return RedirectToAction("Take", "Evaluation", new { trainingId });
}

        var training = await _db.Trainings.FindAsync(trainingId);
        if (training == null) return NotFound();

        var questions = await _db.Questions
            .Where(q => q.TrainingId == trainingId)
            .OrderBy(q => q.Id)
            .ToListAsync();

        ViewBag.Training      = training;
        ViewBag.Questions     = questions;
        var session = await StartOrResumeSessionAsync(
            userId, trainingId, "PostTest", attempts.Count + 1, training.PosttestTimer);

        // Time ran out while the participant was away: grade what was saved
        if (session.ExpiresAt != null && session.ExpiresAt <= DateTime.UtcNow)
        {
            var finished = await RecordAttemptAsync(session, ReadAnswers(session), auto: true);
            TempData["Info"] = "Your time ran out while you were away. Your saved answers were submitted.";
            return RedirectToResult(finished);
        }

        ViewBag.TestType      = "PostTest";
        ViewBag.AttemptNumber = session.AttemptNumber;
        ViewBag.Timer         = training.PosttestTimer; // minutes
        ViewBag.SecondsLeft   = session.ExpiresAt == null
            ? (int?)null
            : Math.Max(0, (int)(session.ExpiresAt.Value - DateTime.UtcNow).TotalSeconds);
        ViewBag.SavedAnswers  = ReadAnswers(session);

        return View("~/Views/Employee/TakeTest.cshtml");
    }

    // ── POST: /Employee/SubmitTest ────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitTest(int trainingId, string testType,
        int attemptNumber, IFormCollection form)
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");
        var userId = GetUserId();

        // The attempt is the one the server started. The posted attemptNumber is ignored.
        var session = await _db.TestSessions.FirstOrDefaultAsync(s =>
            s.UserId == userId && s.TrainingId == trainingId &&
            s.TestType == testType && s.ModuleId == null && s.Status == "InProgress");

        if (session == null)
        {
            // Double click or refresh after submitting: show the result that already exists
            var last = await _db.ParticipantTestAttempts.AsNoTracking()
                .Where(a => a.UserId == userId && a.TrainingId == trainingId && a.TestType == testType)
                .OrderByDescending(a => a.TakenAt)
                .FirstOrDefaultAsync();
            if (last != null) return RedirectToResult(last);

            TempData["Error"] = "We could not find an active test. Please start it again from your dashboard.";
            return RedirectToAction("Dashboard");
        }

        var answers = ReadAnswers(session);

        // Within the deadline (plus a short grace period) the answers on the page win over the last autosave.
        // Past it, only what was saved before the deadline counts.
        var pastDeadline = session.ExpiresAt != null
            && DateTime.UtcNow > session.ExpiresAt.Value.AddSeconds(TestGraceSeconds);
        if (!pastDeadline)
        {
            foreach (var key in form.Keys.Where(IsAnswerKey))
                answers[key] = form[key].ToString();
        }

        var attempt = await RecordAttemptAsync(session, answers, auto: pastDeadline);
        return RedirectToResult(attempt);
    }

    // ── GET: /Employee/TestResult ─────────────────────────────────
    public async Task<IActionResult> TestResult(int trainingId, string testType,
        decimal score, decimal totalPoints, int isPassed, int attemptNumber)
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var training = await _db.Trainings.FindAsync(trainingId);
        var userId   = GetUserId();

        // Remaining post-test attempts
        var usedAttempts = await _db.ParticipantTestAttempts
            .CountAsync(a => a.UserId     == userId
                          && a.TrainingId == trainingId
                          && a.TestType   == "PostTest");

        // Use the saved attempt, not the numbers in the address bar
        var savedAttempt = await _db.ParticipantTestAttempts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.TrainingId == trainingId
                                   && a.TestType == testType && a.AttemptNumber == attemptNumber);
        if (savedAttempt != null)
        {
            score       = savedAttempt.Score;
            totalPoints = savedAttempt.TotalPoints;
            isPassed    = savedAttempt.IsPassed ? 1 : 0;
        }

        ViewBag.Training        = training;
        ViewBag.TestType        = testType;
        ViewBag.Score           = score;
        ViewBag.TotalPoints     = totalPoints;
        ViewBag.IsPassed        = isPassed == 1;
        ViewBag.AttemptNumber   = attemptNumber;
        ViewBag.AttemptsUsed    = usedAttempts;
        ViewBag.AttemptsLeft    = 3 - usedAttempts;

        return View("~/Views/Employee/TestResult.cshtml");
    }

    // ── GET: /Employee/Profile ────────────────────────────────────
    public async Task<IActionResult> Profile()
    {
        var userId = GetUserId();
         if (userId == 0) return RedirectToAction("Login", "Home");
        SetViewBag();

        

        var user = await _db.Users
            .Include(u => u.Region)
            .Include(u => u.Office)
            .Include(u => u.Division)
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null) return RedirectToAction("Login", "Home");

        ViewBag.Trainings = await _db.TrainingParticipants
            .Include(tp => tp.Training)
            .Where(tp => tp.UserId == userId)
            .OrderByDescending(tp => tp.EnrollmentDate)
            .ToListAsync();

        return View("~/Views/Employee/Profile.cshtml", user);
    }

    // ── POST: /Employee/UpdateProfile ─────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(User model)
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");

        var userId = GetUserId();
        var user   = await _db.Users.FindAsync(userId);

        if (user == null) return RedirectToAction("Login", "Home");

        user.FirstName     = model.FirstName;
        user.MiddleName    = model.MiddleName;
        user.LastName      = model.LastName;
        user.ContactNumber = model.ContactNumber;
        user.Gender        = model.Gender;
        user.Age           = model.Age;
        user.UpdatedAt     = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        HttpContext.Session.SetString("UserName",
            $"{user.FirstName} {user.LastName}");

        TempData["Success"] = "Profile updated successfully!";
        return RedirectToAction("Profile");
    }

    // ── GET: /Employee/Grade ─────────────────────────────────────
// REPLACE the existing Grade action in EmployeeController.cs with this:

public async Task<IActionResult> Grade()
{
    if (!IsEmployee()) return RedirectToAction("Login", "Home");
    SetViewBag();

    var userId = GetUserId();

    var enrollments = await _db.TrainingParticipants
        .Include(tp => tp.Training)
        .Where(tp => tp.UserId == userId)
        .OrderByDescending(tp => tp.Training.StartDate)
        .ToListAsync();

    var trainingIds = enrollments.Select(tp => tp.TrainingId).ToList();

    var attempts = await _db.ParticipantTestAttempts
        .Where(a => a.UserId == userId && trainingIds.Contains(a.TrainingId))
        .ToListAsync();

    var quizAttempts = await _db.QuizAttempts
        .Include(qa => qa.Quiz)
        .Include(qa => qa.Module)
        .Where(qa => qa.UserId == userId && trainingIds.Contains(qa.TrainingId))
        .ToListAsync();

    var questions = await _db.Questions
        .Where(q => trainingIds.Contains(q.TrainingId))
        .ToListAsync();

    var gradeRows = enrollments.Select(tp =>
    {
        var trainingId = tp.TrainingId;

        var preTest = attempts
            .Where(a => a.TrainingId == trainingId && a.TestType == "PreTest")
            .OrderByDescending(a => a.TakenAt)
            .FirstOrDefault();

        var postTests = attempts
            .Where(a => a.TrainingId == trainingId && a.TestType == "PostTest")
            .OrderByDescending(a => a.Score)
            .ToList();

        var bestPost  = postTests.FirstOrDefault();
        var totalPoints = questions
            .Where(q => q.TrainingId == trainingId)
            .Sum(q => q.Points);

        return new EmployeeGradeViewModel
        {
            Training         = tp.Training,
            TrainingId       = trainingId,
            PreTestScore     = preTest?.Score,
            PostTestScore    = bestPost?.Score,
            PostTestPassed   = bestPost?.IsPassed ?? false,
            PostAttempts     = postTests.Count,
            TotalPoints      = totalPoints,
            CompletionStatus = tp.CompletionStatus,
            EnrollmentDate   = tp.EnrollmentDate,
            QuizAttempts     = quizAttempts
                .Where(qa => qa.TrainingId == trainingId)
                .ToList()
        };
    }).ToList();

    ViewBag.GradeRows = gradeRows;
    return View("~/Views/Employee/Grade.cshtml");
}



    // ── GET: /Employee/TakeQuiz ───────────────────────────────────
public async Task<IActionResult> TakeQuiz(int moduleId)
{
    if (!IsEmployee()) return RedirectToAction("Login", "Home");
    SetViewBag();

    var userId = GetUserId();

    // Load quiz with questions and choices
    var quiz = await _db.Quizzes
        .Include(q => q.Questions)
            .ThenInclude(q => q.Choices)
        .FirstOrDefaultAsync(q => q.ModuleId == moduleId);

    if (quiz == null)
    {
        TempData["Error"] = "Quiz not found.";
        return RedirectToAction("Dashboard");
    }

    // Load module for back navigation
    var module = await _db.TrainingModules.FindAsync(moduleId);

    ViewBag.Module = module;

    // Keep the participant's selected answers in the database while they work
    if (module != null)
    {
        var quizSession = await _db.TestSessions.FirstOrDefaultAsync(s =>
            s.UserId == userId && s.ModuleId == moduleId &&
            s.TestType == "Quiz" && s.Status == "InProgress");
        if (quizSession == null)
        {
            quizSession = new TestSession
            {
                UserId = userId, TrainingId = module.TrainingId, ModuleId = moduleId,
                TestType = "Quiz", AttemptNumber = 1, Status = "InProgress"
            };
            _db.TestSessions.Add(quizSession);
            await _db.SaveChangesAsync();
        }
        ViewBag.SavedAnswers = ReadAnswers(quizSession);
    }

    return View("~/Views/Employee/TakeQuiz.cshtml", quiz);
}
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SubmitQuiz(int moduleId, int quizId,
    int trainingId, IFormCollection form)
{
    if (!IsEmployee()) return RedirectToAction("Login", "Home");
 
    var userId = GetUserId();
 
    var quiz = await _db.Quizzes
        .Include(q => q.Questions)
            .ThenInclude(q => q.Choices)
        .FirstOrDefaultAsync(q => q.QuizId == quizId);
 
    if (quiz == null) return NotFound();
 
    int correct = 0;
    int total   = quiz.Questions.Count;
 
    foreach (var question in quiz.Questions)
    {
        var selectedChoiceId = form[$"answer_{question.QuestionId}"].ToString();
        if (string.IsNullOrEmpty(selectedChoiceId)) continue;
 
        if (int.TryParse(selectedChoiceId, out int choiceId))
        {
            var choice = question.Choices.FirstOrDefault(c => c.ChoiceId == choiceId);
            if (choice?.IsCorrect == true) correct++;
        }
    }
 
    int scorePercentage = total > 0 ? (int)Math.Round((double)correct / total * 100) : 0;
    bool passed         = scorePercentage >= quiz.PassingScore;

    // Close the saved-progress session for this quiz
    var openQuiz = await _db.TestSessions.FirstOrDefaultAsync(s =>
        s.UserId == userId && s.ModuleId == moduleId &&
        s.TestType == "Quiz" && s.Status == "InProgress");
    if (openQuiz != null) { openQuiz.Status = "Submitted"; openQuiz.LastSavedAt = DateTime.UtcNow; }
 
    // ── Save to database ──
   var existing = await _db.QuizAttempts
    .FirstOrDefaultAsync(qa => qa.UserId == userId && qa.ModuleId == moduleId);

if (existing != null)
{
    // Only update if new score is better
    if (scorePercentage >= existing.ScorePercentage)
    {
        existing.CorrectCount    = correct;
        existing.TotalQuestions  = total;
        existing.ScorePercentage = scorePercentage;
        existing.IsPassed        = passed;
        existing.TakenAt         = DateTime.UtcNow;
    }
}
else
{
    _db.QuizAttempts.Add(new QuizAttempt
    {
        UserId          = userId,
        ModuleId        = moduleId,
        QuizId          = quizId,
        TrainingId      = trainingId,
        CorrectCount    = correct,
        TotalQuestions  = total,
        ScorePercentage = scorePercentage,
        IsPassed        = passed,
        TakenAt         = DateTime.UtcNow
    });
}
 
   await _db.SaveChangesAsync();

// ── Only mark module as complete if quiz is PASSED ──
if (passed)
{
    var alreadyDone = await _db.ModuleProgresses
        .AnyAsync(mp => mp.UserId == userId && mp.ModuleId == moduleId);

    if (!alreadyDone)
    {
        _db.ModuleProgresses.Add(new ModuleProgress
        {
            UserId      = userId,
            ModuleId    = moduleId,
            TrainingId  = trainingId,
            CompletedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}

TempData["QuizResult"]   = $"{correct}/{total}";
TempData["QuizScore"]    = scorePercentage;
TempData["QuizPassed"]   = passed;
TempData["QuizTitle"]    = quiz.QuizTitle;
TempData["PassingScore"] = quiz.PassingScore;
TempData["ModuleId"]     = moduleId;
TempData["TrainingId"]   = trainingId;

return RedirectToAction("QuizResult");
}
 
// ── GET: /Employee/QuizResult ─────────────────────────────────
public IActionResult QuizResult()
{
    if (!IsEmployee()) return RedirectToAction("Login", "Home");
    SetViewBag();
    return View("~/Views/Employee/QuizResult.cshtml");
}
 
// ── GET: /Employee/QuizResult/{moduleId} — view past quiz score ──
// (used by the "View Score" link on Dashboard for already-completed quizzes)
[HttpGet("Employee/QuizResultByModule/{moduleId}")]
public async Task<IActionResult> QuizResultByModule(int moduleId)
{
    if (!IsEmployee()) return RedirectToAction("Login", "Home");
    SetViewBag();
 
    var userId = GetUserId();
 
    var attempt = await _db.QuizAttempts
        .Include(qa => qa.Quiz)
        .FirstOrDefaultAsync(qa => qa.UserId == userId && qa.ModuleId == moduleId);
 
    if (attempt == null)
    {
        TempData["Error"] = "No quiz attempt found.";
        return RedirectToAction("Dashboard");
    }
 
    TempData["QuizResult"]   = $"{attempt.CorrectCount}/{attempt.TotalQuestions}";
    TempData["QuizScore"]    = attempt.ScorePercentage;
    TempData["QuizPassed"]  = attempt.IsPassed;
    TempData["QuizTitle"]   = attempt.Quiz?.QuizTitle ?? "Quiz";
    TempData["PassingScore"] = attempt.Quiz?.PassingScore ?? 0;
    TempData["ModuleId"]    = moduleId;
    TempData["TrainingId"]  = attempt.TrainingId;
 
    return View("~/Views/Employee/QuizResult.cshtml");
}

// ── POST: /Employee/MarkModuleDone ────────────────────────────
[HttpPost]
public async Task<IActionResult> MarkModuleDone(int moduleId, int trainingId)
{
    if (!IsEmployee()) return Json(new { success = false });

    var userId = GetUserId();

    var exists = await _db.ModuleProgresses
        .AnyAsync(mp => mp.UserId == userId && mp.ModuleId == moduleId);

    if (!exists)
    {
        _db.ModuleProgresses.Add(new ModuleProgress
        {
            UserId      = userId,
            ModuleId    = moduleId,
            TrainingId  = trainingId,
            CompletedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    return Json(new { success = true });
}

}