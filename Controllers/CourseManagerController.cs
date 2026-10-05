using Microsoft.AspNetCore.Mvc;
using LDHub.Data;
using LDHub.Models;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Controllers;

public class CourseManagerController : Controller
{
    private readonly AppDbContext _db;

    public CourseManagerController(AppDbContext db)
    {
        _db = db;
    }
    private int GetUserId()
{
    var str = HttpContext.Session.GetString("UserId");
    return int.TryParse(str, out int id) ? id : 0;
}

    // ── Auth check helper ─────────────────────────────────────────
    private bool IsCourseManager() =>
        HttpContext.Session.GetString("UserRole") == "CourseManager";

    private bool CanManageModules()
    {
        var role = HttpContext.Session.GetString("UserRole");
        return role == "CourseManager" || role == "ResourcePerson";
    }

    // ── ViewBag helper ────────────────────────────────────────────
    private void SetViewBag()
    {
        ViewBag.ActiveRole = "CourseManager";
        ViewBag.UserName = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto = HttpContext.Session.GetString("UserPhoto");
    }

    // ── GET: /CourseManager/Dashboard ─────────────────────────────

public async Task<IActionResult> Dashboard()
{
    if (!IsCourseManager()) return RedirectToAction("Login", "Home");
    SetViewBag();
 
    var now = DateTime.Now;
 
    // ── All trainings ─────────────────────────────────────────────
    var trainings = await _db.Trainings
        .Include(t => t.TrainingParticipants)
            .ThenInclude(tp => tp.User)
        .OrderByDescending(t => t.CreatedAt)
        .ToListAsync();
 
    // ── Stats ─────────────────────────────────────────────────────
    var totalTrainings     = trainings.Count;
    var ongoingTrainings   = trainings.Count(t => t.StartDate <= now && t.EndDate >= now);
    var upcomingTrainings  = trainings.Count(t => t.StartDate > now);
    var completedTrainings = trainings.Count(t => t.EndDate < now);
 
    var totalParticipants  = trainings
        .SelectMany(t => t.TrainingParticipants)
        .Select(tp => tp.UserId)
        .Distinct()
        .Count();
 
    var completedParticipants = trainings
        .SelectMany(t => t.TrainingParticipants)
        .Count(tp => tp.CompletionStatus == "Completed");
 
    var enrolledParticipants = trainings
        .SelectMany(t => t.TrainingParticipants)
        .Count(tp => tp.CompletionStatus == "Enrolled" || tp.CompletionStatus == "In Progress");
 
    // ── Chart data: participants per training (top 6) ─────────────
    var participantsPerTraining = trainings
        .OrderByDescending(t => t.TrainingParticipants.Count)
        .Take(6)
        .Select(t => new
        {
            Title = t.TrainingTitle.Length > 25
                ? t.TrainingTitle.Substring(0, 25) + "..."
                : t.TrainingTitle,
            Count = t.TrainingParticipants.Count
        }).ToList();
 
    // ── Chart data: completion status breakdown ────────────────────
    var statusBreakdown = new
    {
        Completed  = completedParticipants,
        Enrolled   = enrolledParticipants,
        InProgress = trainings
            .SelectMany(t => t.TrainingParticipants)
            .Count(tp => tp.CompletionStatus == "In Progress")
    };
 
    ViewBag.Trainings              = trainings;
    ViewBag.TotalTrainings         = totalTrainings;
    ViewBag.OngoingTrainings       = ongoingTrainings;
    ViewBag.UpcomingTrainings      = upcomingTrainings;
    ViewBag.CompletedTrainings     = completedTrainings;
    ViewBag.TotalParticipants      = totalParticipants;
    ViewBag.CompletedParticipants  = completedParticipants;
    ViewBag.EnrolledParticipants   = enrolledParticipants;
    ViewBag.ParticipantsPerTraining = System.Text.Json.JsonSerializer.Serialize(participantsPerTraining);
ViewBag.StatusBreakdown        = System.Text.Json.JsonSerializer.Serialize(statusBreakdown);
    return View("~/Views/CourseManager/Dashboard.cshtml");
}

    // ── GET: /CourseManager/Trainings ─────────────────────────────
    public async Task<IActionResult> Trainings()
    {
        if (!IsCourseManager()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var trainings = await _db.Trainings
            .Include(t => t.TrainingParticipants)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return View("~/Views/Training/Index.cshtml", trainings);
    }

  

    // ── GET: /CourseManager/TestResult ────────────────────────────
    public IActionResult TestResult()
    {
        if (!IsCourseManager()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/CourseManager/TestResult.cshtml");
    }

    // ── GET: /CourseManager/TrainingReport ────────────────────────
    public IActionResult TrainingReport()
    {
        if (!IsCourseManager()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Training/TrainingReport.cshtml");
    }

    // ── GET: /CourseManager/EvaluationDashboard ───────────────────
    public IActionResult EvaluationDashboard()
    {
        if (!IsCourseManager()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/CourseManager/EvaluationDashboard.cshtml");
    }

    // ── GET: /CourseManager/AutoPdf ───────────────────────────────
    public IActionResult AutoPdf()
    {
        if (!IsCourseManager()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/CourseManager/AutoPdf.cshtml");
    }
}