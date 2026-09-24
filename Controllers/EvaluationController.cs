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

    // ── GET: /Evaluation/Index ─────────────────────────────
    public async Task<IActionResult> Index(int? trainingId)
    {
        if (!CanAccess()) return RedirectToAction("Login", "Home");
        SetViewBag();

        ViewBag.Trainings  = await _db.Trainings.OrderByDescending(t => t.CreatedAt).ToListAsync();
        ViewBag.TrainingId = trainingId;

        if (trainingId == null) return View();

        var training = await _db.Trainings.FindAsync(trainingId);
        if (training == null) return View();

        var questions = await _db.EvaluationQuestions
            .Where(q => q.TrainingId == trainingId)
            .OrderBy(q => q.SortOrder)
            .ToListAsync();

        ViewBag.SelectedTraining = training;
        ViewBag.Questions        = questions;

        return View();
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
}