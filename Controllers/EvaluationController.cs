// Controllers/EvaluationController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;

namespace LDHub.Controllers;

public class EvaluationController : Controller
{
    private readonly AppDbContext _db;
    public EvaluationController(AppDbContext db) => _db = db;

    private bool CanAccess()
    {
        var role = HttpContext.Session.GetString("UserRole");
        return role == "Admin" || role == "CourseManager" || role == "Raam";
    }

    private void SetViewBag()
    {
        ViewBag.ActiveRole = HttpContext.Session.GetString("UserRole");
        ViewBag.UserName   = HttpContext.Session.GetString("UserName");
    }

    // ── GET: /Evaluation/Index ─────────────────────────────// ── GET: /Evaluation/Index ─────────────────────────────
public async Task<IActionResult> Index(int? trainingId)
{
    if (!CanAccess()) return RedirectToAction("Login", "Home");
    SetViewBag();

    var role     = HttpContext.Session.GetString("UserRole");
    var regionId = await GetRaamRegionId();

    IQueryable<Training> trainingsQuery = _db.Trainings;

    if (role == "Raam")
        trainingsQuery = trainingsQuery.Where(t => t.RegionId == regionId);

    ViewBag.Trainings  = await trainingsQuery.OrderByDescending(t => t.CreatedAt).ToListAsync();
    ViewBag.TrainingId = trainingId;

    if (trainingId == null) return View();

    var training = await _db.Trainings.FindAsync(trainingId);
    if (training == null) return View();

    // ── Block RAAM from managing evaluation questions outside their region ──
    if (role == "Raam" && training.RegionId != regionId)
    {
        TempData["Error"] = "You do not have access to this training's evaluation.";
        return RedirectToAction("Index");
    }

    var questions = await _db.EvaluationQuestions
        .Where(q => q.TrainingId == trainingId)
        .OrderBy(q => q.SortOrder)
        .ToListAsync();

    ViewBag.SelectedTraining = training;
    ViewBag.Questions        = questions;

    return View();
}

private async Task<int?> GetRaamRegionId()
{
    var role = HttpContext.Session.GetString("UserRole");
    if (role != "Raam") return null;

    var userIdStr = HttpContext.Session.GetString("UserId");
    if (!int.TryParse(userIdStr, out int userId)) return null;

    var user = await _db.Users.FindAsync(userId);
    return user?.RegionId;
}

    // ── POST: /Evaluation/SaveQuestion ─────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQuestion(
        int questionId, int trainingId, string questionText, string questionType,
        string? choiceA, string? choiceB, string? choiceC, string? choiceD, bool isRequired)
    {
        if (!CanAccess()) return RedirectToAction("Login", "Home");

        if (questionId == 0)
        {
            var maxOrder = await _db.EvaluationQuestions
                .Where(q => q.TrainingId == trainingId)
                .Select(q => (int?)q.SortOrder)
                .MaxAsync() ?? 0;

            _db.EvaluationQuestions.Add(new EvaluationQuestion
            {
                TrainingId   = trainingId,
                QuestionText = questionText,
                QuestionType = questionType,
                ChoiceA      = choiceA,
                ChoiceB      = choiceB,
                ChoiceC      = choiceC,
                ChoiceD      = choiceD,
                IsRequired   = isRequired,
                SortOrder    = maxOrder + 1
            });
            TempData["Success"] = "Question added!";
        }
        else
        {
            var q = await _db.EvaluationQuestions.FindAsync(questionId);
            if (q != null)
            {
                q.QuestionText = questionText;
                q.QuestionType = questionType;
                q.ChoiceA      = choiceA;
                q.ChoiceB      = choiceB;
                q.ChoiceC      = choiceC;
                q.ChoiceD      = choiceD;
                q.IsRequired   = isRequired;
            }
            TempData["Success"] = "Question updated!";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction("Index", new { trainingId });
    }

    // ── POST: /Evaluation/DeleteQuestion ───────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(int questionId, int trainingId)
    {
        if (!CanAccess()) return RedirectToAction("Login", "Home");

        var q = await _db.EvaluationQuestions.FindAsync(questionId);
        if (q != null)
        {
            _db.EvaluationQuestions.Remove(q);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Question deleted!";
        }

        return RedirectToAction("Index", new { trainingId });
    }

    // ADD these actions to EvaluationController.cs

// ── GET: /Evaluation/Take ──────────────────────────────────
public async Task<IActionResult> Take(int trainingId)
{
    var userIdStr = HttpContext.Session.GetString("UserId");
    if (!int.TryParse(userIdStr, out int userId))
        return RedirectToAction("Login", "Home");

    SetViewBag();

    // Check if already submitted
    var alreadySubmitted = await _db.EvaluationResponses
        .AnyAsync(r => r.TrainingId == trainingId && r.UserId == userId);

    if (alreadySubmitted)
    {
        TempData["Success"] = "You have already submitted the evaluation for this training.";
        return RedirectToAction("Dashboard", "Employee");
    }

    var training = await _db.Trainings.FindAsync(trainingId);
    if (training == null) return NotFound();

    var questions = await _db.EvaluationQuestions
        .Where(q => q.TrainingId == trainingId)
        .OrderBy(q => q.SortOrder)
        .ToListAsync();

    ViewBag.Training  = training;
    ViewBag.Questions = questions;

    return View("~/Views/Evaluation/Take.cshtml");
}

// ── POST: /Evaluation/Submit ───────────────────────────────
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Submit(int trainingId, IFormCollection form)
{
    var userIdStr = HttpContext.Session.GetString("UserId");
    if (!int.TryParse(userIdStr, out int userId))
        return RedirectToAction("Login", "Home");

    // Prevent duplicate submission
    var alreadySubmitted = await _db.EvaluationResponses
        .AnyAsync(r => r.TrainingId == trainingId && r.UserId == userId);

    if (alreadySubmitted)
    {
        TempData["Success"] = "You have already submitted the evaluation.";
        return RedirectToAction("Dashboard", "Employee");
    }

    // Create response
    var response = new EvaluationResponse
    {
        TrainingId  = trainingId,
        UserId      = userId,
        SubmittedAt = DateTime.UtcNow
    };

    _db.EvaluationResponses.Add(response);
    await _db.SaveChangesAsync();

    // Save answers
    var questionIds = form["question_ids"].Select(id => int.Parse(id)).ToList();

    foreach (var qId in questionIds)
    {
        var question = await _db.EvaluationQuestions.FindAsync(qId);
        if (question == null) continue;

        var answer = new EvaluationAnswer
        {
            EvaluationResponseId  = response.Id,
            EvaluationQuestionId  = qId
        };

        if (question.QuestionType == "rating")
        {
            var ratingStr = form[$"rating_{qId}"].ToString();
            if (int.TryParse(ratingStr, out int rating))
                answer.RatingValue = rating;
        }
        else if (question.QuestionType == "text")
        {
            answer.AnswerText = form[$"text_{qId}"].ToString();
        }
        else if (question.QuestionType == "multiple_choice")
        {
            answer.AnswerText = form[$"mc_{qId}"].ToString();
        }

        _db.EvaluationAnswers.Add(answer);
    }

    await _db.SaveChangesAsync();

    TempData["Success"] = "Evaluation submitted successfully! You may now take the Post-Test.";
    return RedirectToAction("Dashboard", "Employee");
}

// ── HELPER: Check if evaluation is completed ───────────────
// Call this from EmployeeController before allowing Post-Test
public static async Task<bool> HasCompletedEvaluation(
    AppDbContext db, int userId, int trainingId)
{
    return await db.EvaluationResponses
        .AnyAsync(r => r.UserId == userId && r.TrainingId == trainingId);
}
}