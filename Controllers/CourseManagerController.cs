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
    public IActionResult Dashboard()
    {
        if (!IsCourseManager()) return RedirectToAction("Login", "Home");
        SetViewBag();
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