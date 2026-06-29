using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;

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
        // TEMP DEBUG
Console.WriteLine($"SESSION UserId: {HttpContext.Session.GetInt32("UserId")}");
Console.WriteLine($"GetUserId() returns: {userId}");

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

        // ── Get current active enrollment ──
        var currentEnrollment = enrollments
            .Where(tp => tp.CompletionStatus != "Completed"
                    && (tp.Training?.EndDate == null || tp.Training.EndDate >= now))
            .OrderByDescending(tp => tp.Training?.StartDate)
            .FirstOrDefault();

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

        return View("~/Views/Employee/Dashboard.cshtml", enrollments);
    }

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

        return View("~/Views/Employee/TakeTest.cshtml");
    }

    // ── GET: /Employee/PostTest ───────────────────────────────────
    public async Task<IActionResult> PostTest(int trainingId)
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var userId = GetUserId();

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

        var training = await _db.Trainings.FindAsync(trainingId);
        if (training == null) return NotFound();

        var questions = await _db.Questions
            .Where(q => q.TrainingId == trainingId)
            .OrderBy(q => q.Id)
            .ToListAsync();

        ViewBag.Training      = training;
        ViewBag.Questions     = questions;
        ViewBag.TestType      = "PostTest";
        ViewBag.AttemptNumber = attempts.Count + 1;
        ViewBag.Timer         = training.PosttestTimer; // minutes

        return View("~/Views/Employee/TakeTest.cshtml");
    }

    // ── POST: /Employee/SubmitTest ────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitTest(int trainingId, string testType,
        int attemptNumber, IFormCollection form)
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");

        var userId   = GetUserId();
        var training = await _db.Trainings.FindAsync(trainingId);
        if (training == null) return NotFound();

        var questions = await _db.Questions
            .Where(q => q.TrainingId == trainingId)
            .ToListAsync();

        decimal score       = 0;
        decimal totalPoints = questions.Sum(q => q.Points);

        foreach (var q in questions)
        {
            var answer = form[$"answer_{q.Id}"].ToString().Trim();
            if (string.IsNullOrEmpty(answer)) continue;

            bool correct = q.QuestionType switch
            {
                "true_false"      => answer.Equals(q.CorrectAnswer, StringComparison.OrdinalIgnoreCase),
                "multiple_choice" => answer.Equals(q.CorrectAnswer, StringComparison.OrdinalIgnoreCase),
                "identification"  => answer.Equals(q.CorrectAnswer, StringComparison.OrdinalIgnoreCase),
                _                 => false
            };

            if (correct) score += q.Points;
        }

        bool isPassed = testType == "PostTest"
            ? score >= training.MinimumScore
            : false; // pre-test never gates

        var attempt = new ParticipantTestAttempt
        {
            TrainingId    = trainingId,
            UserId        = userId,
            TestType      = testType,
            Score         = score,
            TotalPoints   = totalPoints,
            IsPassed      = isPassed,
            AttemptNumber = attemptNumber,
            TakenAt       = DateTime.UtcNow
        };

        _db.ParticipantTestAttempts.Add(attempt);

        // If post-test passed → mark training as Completed
        if (testType == "PostTest" && isPassed)
        {
            var enrollment = await _db.TrainingParticipants
                .FirstOrDefaultAsync(tp => tp.TrainingId == trainingId
                                        && tp.UserId     == userId);
            if (enrollment != null)
                enrollment.CompletionStatus = "Completed";
        }

        // If post-test and first attempt → mark as In Progress
        if (testType == "PostTest" && attemptNumber == 1)
        {
            var enrollment = await _db.TrainingParticipants
                .FirstOrDefaultAsync(tp => tp.TrainingId == trainingId
                                        && tp.UserId     == userId);
            if (enrollment != null && enrollment.CompletionStatus == "Enrolled")
                enrollment.CompletionStatus = "In Progress";
        }

        await _db.SaveChangesAsync();

        // Redirect to results page
        return RedirectToAction("TestResult", new
        {
            trainingId,
            testType,
            score         = (int)score,
            totalPoints   = (int)totalPoints,
            isPassed      = isPassed ? 1 : 0,
            attemptNumber
        });
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
        if (!IsEmployee()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var userId = GetUserId();

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

    // ── GET: /Employee/Grade ──────────────────────────────────────
    public async Task<IActionResult> Grade()
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var userId = GetUserId();

        var enrollments = await _db.TrainingParticipants
            .Include(tp => tp.Training)
            .Where(tp => tp.UserId == userId)
            .OrderByDescending(tp => tp.EnrollmentDate)
            .ToListAsync();

        return View("~/Views/Employee/Grade.cshtml", enrollments);
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
    return View("~/Views/Employee/TakeQuiz.cshtml", quiz);
}
// ── POST: /Employee/SubmitQuiz ────────────────────────────────
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

    TempData["QuizResult"]    = $"{correct}/{total}";
    TempData["QuizScore"]     = scorePercentage;
    TempData["QuizPassed"]    = passed;
    TempData["QuizTitle"]     = quiz.QuizTitle;
    TempData["PassingScore"]  = quiz.PassingScore;
    TempData["ModuleId"]      = moduleId;
    TempData["TrainingId"]    = trainingId;

    return RedirectToAction("QuizResult");
}

// ── GET: /Employee/QuizResult ─────────────────────────────────
public IActionResult QuizResult()
{
    if (!IsEmployee()) return RedirectToAction("Login", "Home");
    SetViewBag();
    return View("~/Views/Employee/QuizResult.cshtml");
}
// POST: /Employee/MarkModuleDone
[HttpPost]
public async Task<IActionResult> MarkModuleDone(int moduleId, int trainingId)
{
    var userId = GetUserId();

    var already = await _db.ModuleProgresses
        .AnyAsync(mp => mp.UserId    == userId
                     && mp.ModuleId  == moduleId
                     && mp.TrainingId == trainingId);

    if (!already)
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

    return Ok();
}
}