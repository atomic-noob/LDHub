using Microsoft.AspNetCore.Mvc;
using LDHub.Data;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Controllers;

public class CourseManagerController : Controller
{
    // Database connection — like $conn in PHP
    private readonly AppDbContext _db;

    public CourseManagerController(AppDbContext db)
    {
        _db = db;
    }

    // ── Auth check helper ─────────────────────────────────────────
    // Checks if current session has CourseManager role
    private bool IsCourseManager() =>
        HttpContext.Session.GetString("UserRole") == "CourseManager";

    // ── ViewBag helper ────────────────────────────────────────────
    // Sets common data needed by layout and sidebar
    private void SetViewBag()
    {
        ViewBag.ActiveRole = "CourseManager";
        ViewBag.UserName = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto = HttpContext.Session.GetString("UserPhoto");
    }

    // ── GET: /CourseManager/Dashboard ─────────────────────────────
    // Main landing page after switching to CourseManager role
    // If not CourseManager → redirect to login
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
                .Include(t => t.TrainingInstructors)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View("~/Views/CourseManager/Trainings.cshtml", trainings);
        }

    // ── GET: /CourseManager/Modules ───────────────────────────────
    public IActionResult Modules()
    {
        if (!IsCourseManager()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Module/index.cshtml");
    }

    // ── GET: /CourseManager/TestMaker ─────────────────────────────
    public IActionResult TestMaker()
    {
        if (!IsCourseManager()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/CourseManager/TestMaker.cshtml");
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
        return View("~/Views/CourseManager/TrainingReport.cshtml");
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