using LDHub.Helpers;
using LDHub.Services.Interfaces;
using LDHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;
using LDHub.Models.ViewModels;

namespace LDHub.Controllers;

public class EmployeeController : Controller
{
    private readonly AppDbContext _db;
    private readonly ITestSessionService _tests;

    public EmployeeController(AppDbContext db, ITestSessionService tests)
    {
        _db = db;
        _tests = tests;
    }

    // ── Auth check helper ─────────────────────────────────────────
    private bool IsEmployee() =>
        HttpContext.Session.GetString("UserRole") == AppRoles.Employee;

    // ── Get current logged in user ID from session ─────────────────
    private int GetUserId()
{
    var str = HttpContext.Session.GetString("UserId");
    return int.TryParse(str, out int id) ? id : 0;
}

    // ── ViewBag helper ────────────────────────────────────────────
    private void SetViewBag()
    {
        ViewBag.ActiveRole = AppRoles.Employee;
        ViewBag.UserName   = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto  = HttpContext.Session.GetString("UserPhoto");
    }

    // ── GET: /Employee/Dashboard ──────────────────────────────────
    [RequireRole(AppRoles.Employee)]
    public async Task<IActionResult> Dashboard()
    {
        SetViewBag();

        var userId = GetUserId();
        var now    = DateTime.Now;
        // Grade tests whose time ran out while the participant was away
        await _tests.FinalizeExpiredAsync(userId);

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
    && tp.CompletionStatus != EnrollmentStatus.Completed)
            {
                tp.CompletionStatus = EnrollmentStatus.Completed;
                anyUpdated = true;
            }
        }
        if (anyUpdated) await _db.SaveChangesAsync();

       // ── Get current active enrollment (include upcoming ones too) ──
                var currentEnrollment = enrollments
                    .Where(tp => tp.CompletionStatus != EnrollmentStatus.Completed
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
                            && a.TestType   == TestTypes.PreTest);
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
                         && a.TestType   == TestTypes.PostTest)
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
        ViewBag.TotalCompleted  = enrollments.Count(tp => tp.CompletionStatus == EnrollmentStatus.Completed);
        ViewBag.TotalOngoing    = enrollments.Count(tp =>
            tp.Training != null &&
            now >= tp.Training.StartDate &&
            now <= tp.Training.EndDate);
        ViewBag.Enrollment      = currentEnrollment;
        ViewBag.Modules         = modules;
        ViewBag.CompletedCount  = enrollments.Count(tp => tp.CompletionStatus == EnrollmentStatus.Completed);
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

        if (req.TestType is not (TestTypes.PreTest or TestTypes.PostTest or TestTypes.Quiz))
            return Json(new { ok = false });

        var session = await _db.TestSessions.FirstOrDefaultAsync(s =>
            s.UserId == userId && s.TrainingId == req.TrainingId &&
            s.TestType == req.TestType && s.ModuleId == req.ModuleId &&
            s.Status == "InProgress");

        if (session == null) return Json(new { ok = false, noSession = true });

        var now = DateTime.UtcNow;
        if (session.ExpiresAt != null && now > session.ExpiresAt.Value.AddSeconds(TestSessionService.GraceSeconds))
            return Json(new { ok = false, expired = true });

        var clean = (req.Answers ?? new Dictionary<string, string?>())
            .Where(kv => TestSessionService.IsAnswerKey(kv.Key) && (kv.Value?.Length ?? 0) <= 500)
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
    [RequireRole(AppRoles.Employee)]
    public async Task<IActionResult> PreTest(int trainingId)
    {
        SetViewBag();

        var userId = GetUserId();

        // Block if already taken
        var alreadyTaken = await _db.ParticipantTestAttempts
            .AnyAsync(a => a.UserId     == userId
                        && a.TrainingId == trainingId
                        && a.TestType   == TestTypes.PreTest);

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
        ViewBag.TestType  = TestTypes.PreTest;

        var session = await _tests.StartOrResumeAsync(userId, trainingId, "PreTest", 1, 0);
        ViewBag.AttemptNumber = 1;
        ViewBag.SavedAnswers  = TestSessionService.ReadAnswers(session);

        return View("~/Views/Employee/TakeTest.cshtml");
    }

    // ── GET: /Employee/PostTest ───────────────────────────────────
    [RequireRole(AppRoles.Employee)]
    public async Task<IActionResult> PostTest(int trainingId)
    {
        SetViewBag();

        var userId = GetUserId();

        // Grade any earlier post-test whose time ran out while the participant was away
        await _tests.FinalizeExpiredAsync(userId);

        // Check attempt count — max 3
        var attempts = await _db.ParticipantTestAttempts
            .Where(a => a.UserId     == userId
                     && a.TrainingId == trainingId
                     && a.TestType   == TestTypes.PostTest)
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
                        && a.TestType   == TestTypes.PreTest);

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
        var session = await _tests.StartOrResumeAsync(
            userId, trainingId, "PostTest", attempts.Count + 1, training.PosttestTimer);

        // Time ran out while the participant was away: grade what was saved
        if (session.ExpiresAt != null && session.ExpiresAt <= DateTime.UtcNow)
        {
            var finished = await _tests.RecordAttemptAsync(session, TestSessionService.ReadAnswers(session), auto: true);
            TempData["Info"] = "Your time ran out while you were away. Your saved answers were submitted.";
            return RedirectToResult(finished);
        }

        ViewBag.TestType      = TestTypes.PostTest;
        ViewBag.AttemptNumber = session.AttemptNumber;
        ViewBag.Timer         = training.PosttestTimer; // minutes
        ViewBag.SecondsLeft   = session.ExpiresAt == null
            ? (int?)null
            : Math.Max(0, (int)(session.ExpiresAt.Value - DateTime.UtcNow).TotalSeconds);
        ViewBag.SavedAnswers  = TestSessionService.ReadAnswers(session);

        return View("~/Views/Employee/TakeTest.cshtml");
    }

    // ── POST: /Employee/SubmitTest ────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole(AppRoles.Employee)]
    public async Task<IActionResult> SubmitTest(int trainingId, string testType,
        int attemptNumber, IFormCollection form)
    {
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

        var answers = TestSessionService.ReadAnswers(session);

        // Within the deadline (plus a short grace period) the answers on the page win over the last autosave.
        // Past it, only what was saved before the deadline counts.
        var pastDeadline = session.ExpiresAt != null
            && DateTime.UtcNow > session.ExpiresAt.Value.AddSeconds(TestSessionService.GraceSeconds);
        if (!pastDeadline)
        {
            foreach (var key in form.Keys.Where(TestSessionService.IsAnswerKey))
                answers[key] = form[key].ToString();
        }

        var attempt = await _tests.RecordAttemptAsync(session, answers, auto: pastDeadline);
        return RedirectToResult(attempt);
    }

    // ── GET: /Employee/TestResult ─────────────────────────────────
    [RequireRole(AppRoles.Employee)]
    public async Task<IActionResult> TestResult(int trainingId, string testType,
        decimal score, decimal totalPoints, int isPassed, int attemptNumber)
    {
        SetViewBag();

        var training = await _db.Trainings.FindAsync(trainingId);
        var userId   = GetUserId();

        // Remaining post-test attempts
        var usedAttempts = await _db.ParticipantTestAttempts
            .CountAsync(a => a.UserId     == userId
                          && a.TrainingId == trainingId
                          && a.TestType   == TestTypes.PostTest);

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
    [RequireRole(AppRoles.Employee)]
    public async Task<IActionResult> UpdateProfile(User model)
    {

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

[RequireRole(AppRoles.Employee)]
public async Task<IActionResult> Grade()
{
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
            .Where(a => a.TrainingId == trainingId && a.TestType == TestTypes.PreTest)
            .OrderByDescending(a => a.TakenAt)
            .FirstOrDefault();

        var postTests = attempts
            .Where(a => a.TrainingId == trainingId && a.TestType == TestTypes.PostTest)
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
[RequireRole(AppRoles.Employee)]
public async Task<IActionResult> TakeQuiz(int moduleId)
{
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
            s.TestType == TestTypes.Quiz && s.Status == "InProgress");
        if (quizSession == null)
        {
            quizSession = new TestSession
            {
                UserId = userId, TrainingId = module.TrainingId, ModuleId = moduleId,
                TestType = TestTypes.Quiz, AttemptNumber = 1, Status = "InProgress"
            };
            _db.TestSessions.Add(quizSession);
            await _db.SaveChangesAsync();
        }
        ViewBag.SavedAnswers = TestSessionService.ReadAnswers(quizSession);
    }

    return View("~/Views/Employee/TakeQuiz.cshtml", quiz);
}
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Employee)]
public async Task<IActionResult> SubmitQuiz(int moduleId, int quizId,
    int trainingId, IFormCollection form)
{
 
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
        s.TestType == TestTypes.Quiz && s.Status == "InProgress");
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
[RequireRole(AppRoles.Employee)]
public IActionResult QuizResult()
{
    SetViewBag();
    return View("~/Views/Employee/QuizResult.cshtml");
}
 
// ── GET: /Employee/QuizResult/{moduleId} — view past quiz score ──
// (used by the "View Score" link on Dashboard for already-completed quizzes)
[HttpGet("Employee/QuizResultByModule/{moduleId}")]
[RequireRole(AppRoles.Employee)]
public async Task<IActionResult> QuizResultByModule(int moduleId)
{
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
[ValidateAntiForgeryToken]
public async Task<IActionResult> MarkModuleDone(int moduleId, int trainingId)
{
    if (!IsEmployee()) return Json(new { success = false });

    var userId = GetUserId();

    // The module must exist, belong to the posted training, and the user must be enrolled in it
    var module = await _db.TrainingModules.AsNoTracking()
        .Include(m => m.Quiz)
        .FirstOrDefaultAsync(m => m.ModuleId == moduleId);
    if (module == null || module.TrainingId != trainingId)
        return Json(new { success = false });

    var enrolled = await _db.TrainingParticipants
        .AnyAsync(tp => tp.TrainingId == module.TrainingId && tp.UserId == userId);
    if (!enrolled) return Json(new { success = false });

    // Quiz modules are completed by passing the quiz (SubmitQuiz), never by a direct call
    if (module.Quiz != null) return Json(new { success = false });

    var exists = await _db.ModuleProgresses
        .AnyAsync(mp => mp.UserId == userId && mp.ModuleId == moduleId);

    if (!exists)
    {
        _db.ModuleProgresses.Add(new ModuleProgress
        {
            UserId      = userId,
            ModuleId    = moduleId,
            TrainingId  = module.TrainingId,
            CompletedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    return Json(new { success = true });
}

}