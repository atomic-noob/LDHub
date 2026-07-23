using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Data;
using LDHub.Models;

namespace LDHub.Controllers;

public class ResourcePersonController : Controller
{
    private readonly AppDbContext _db;

    public ResourcePersonController(AppDbContext db)
    {
        _db = db;
    }

    private bool IsResourcePerson() =>
        HttpContext.Session.GetString("UserRole") == "ResourcePerson";

    private int GetUserId()
    {
        var str = HttpContext.Session.GetString("UserId");
        return int.TryParse(str, out int id) ? id : 0;
    }

    private void SetViewBag()
    {
        ViewBag.ActiveRole = "ResourcePerson";
        ViewBag.UserName   = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto  = HttpContext.Session.GetString("UserPhoto");
    }

    // ── GET: /ResourcePerson/Index ────────────────────────────────
    public async Task<IActionResult> Index()
    {
        if (!IsResourcePerson()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var userId = GetUserId();
        var now    = DateTime.Now;

        // Get assigned trainings
        var assignedTrainings = await _db.TrainingInstructors
            .Include(ti => ti.Training)
                .ThenInclude(t => t.Region)
            .Include(ti => ti.Training)
                .ThenInclude(t => t.TrainingParticipants)
            .Where(ti => ti.UserId == userId)
            .OrderByDescending(ti => ti.Training.StartDate)
            .ToListAsync();

        var trainings = assignedTrainings.Select(ti => ti.Training).ToList();

        // Stats
        var totalAssigned  = trainings.Count;
        var ongoingCount   = trainings.Count(t =>
            t.StartDate.HasValue && t.EndDate.HasValue &&
            now >= t.StartDate && now <= t.EndDate);
        var completedCount = trainings.Count(t =>
            t.EndDate.HasValue && now > t.EndDate);
        var upcomingCount  = trainings.Count(t =>
            t.StartDate.HasValue && now < t.StartDate);

        // Current active training
        var currentTraining = trainings
            .Where(t => t.StartDate.HasValue && t.EndDate.HasValue &&
                        now >= t.StartDate && now <= t.EndDate)
            .OrderByDescending(t => t.StartDate)
            .FirstOrDefault();

        // Recent 5 trainings
        var recentTrainings = trainings.Take(5).ToList();

        // User info
       var user = await _db.Users
    .Include(u => u.Region)
    .Include(u => u.Office)
    .FirstOrDefaultAsync(u => u.UserId == userId);

if (user == null) return RedirectToAction("Login", "Home");

ViewBag.User = user;
        ViewBag.TotalAssigned   = totalAssigned;
        ViewBag.OngoingCount    = ongoingCount;
        ViewBag.CompletedCount  = completedCount;
        ViewBag.UpcomingCount   = upcomingCount;
        ViewBag.CurrentTraining = currentTraining;
        ViewBag.RecentTrainings = recentTrainings;

        return View("~/Views/ResourcePerson/ResourcePersonIndex.cshtml");
    }

    // ── GET: /ResourcePerson/TrainingHistory ──────────────────────
    public async Task<IActionResult> TrainingHistory()
    {
        if (!IsResourcePerson()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var userId = GetUserId();
        var now    = DateTime.Now;

        var assignedTrainings = await _db.TrainingInstructors
            .Include(ti => ti.Training)
                .ThenInclude(t => t.Region)
            .Include(ti => ti.Training)
                .ThenInclude(t => t.TrainingParticipants)
                    .ThenInclude(tp => tp.User)
            .Where(ti => ti.UserId == userId)
            .OrderByDescending(ti => ti.Training.StartDate)
            .ToListAsync();

        ViewBag.AssignedTrainings = assignedTrainings;
        ViewBag.Now               = now;

        return View("~/Views/ResourcePerson/ResourcePersonTrainingHistory.cshtml");
    }

    // ── GET: /ResourcePerson/Dashboard ───────────────────────────
    public IActionResult Dashboard()
    {
        if (!IsResourcePerson()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return RedirectToAction("Index");
    }

    // ── GET: /ResourcePerson/Grades ──────────────────────────────
    public IActionResult Grades()
    {
        if (!IsResourcePerson()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return RedirectToAction("Index", "Content", new { area = "" });
    }

    // ── GET: /ResourcePerson/TestResult ──────────────────────────
    public IActionResult TestResult()
    {
        if (!IsResourcePerson()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return RedirectToAction("TestMaker", "Content");
    }
}