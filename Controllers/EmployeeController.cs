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
    private int GetUserId() =>
        int.Parse(HttpContext.Session.GetString("UserId") ?? "0");

    // ── ViewBag helper ────────────────────────────────────────────
    private void SetViewBag()
    {
        ViewBag.ActiveRole = "Employee";
        ViewBag.UserName   = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto  = HttpContext.Session.GetString("UserPhoto");
    }

    // ── GET: /Employee/Dashboard ──────────────────────────────────
    // Shows overview — enrolled trainings, completed, ongoing
    public async Task<IActionResult> Dashboard()
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var userId = GetUserId();

        // Get all trainings the employee is enrolled in
        var enrollments = await _db.TrainingParticipants
            .Include(tp => tp.Training)
            .Where(tp => tp.UserId == userId)
            .ToListAsync();

        // Count by status
        ViewBag.TotalEnrolled  = enrollments.Count;
        ViewBag.TotalCompleted = enrollments
            .Count(tp => tp.CompletionStatus == "Completed");
        ViewBag.TotalOngoing   = enrollments
            .Count(tp => tp.Training != null
                      && DateTime.Now >= tp.Training.StartDate
                      && DateTime.Now <= tp.Training.EndDate);

        return View("~/Views/Employee/Dashboard.cshtml", enrollments);
    }

    // ── GET: /Employee/Profile ────────────────────────────────────
    // Shows employee personal info from database
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

            // Pass trainings separately via ViewBag
            ViewBag.Trainings = await _db.TrainingParticipants
                .Include(tp => tp.Training)
                .Where(tp => tp.UserId == userId)
                .OrderByDescending(tp => tp.EnrollmentDate)
                .ToListAsync();

            return View("~/Views/Employee/Profile.cshtml", user);
        }

    // ── POST: /Employee/UpdateProfile ─────────────────────────────
    // Update employee profile info
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(User model)
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");

        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);

        if (user == null) return RedirectToAction("Login", "Home");

        // Only update allowed fields
        user.FirstName     = model.FirstName;
        user.MiddleName    = model.MiddleName;
        user.LastName      = model.LastName;
        user.ContactNumber = model.ContactNumber;
        user.Gender        = model.Gender;
        user.Age           = model.Age;
        user.UpdatedAt     = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Update session name
        HttpContext.Session.SetString("UserName",
            $"{user.FirstName} {user.LastName}");

        TempData["Success"] = "Profile updated successfully!";
        return RedirectToAction("Profile");
    }

    // ── GET: /Employee/Grade ──────────────────────────────────────
    // Shows employee training grades and scores
    public async Task<IActionResult> Grade()
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var userId = GetUserId();

        // Get all trainings with completion status
        var enrollments = await _db.TrainingParticipants
            .Include(tp => tp.Training)
            .Where(tp => tp.UserId == userId)
            .OrderByDescending(tp => tp.EnrollmentDate)
            .ToListAsync();

        return View("~/Views/Employee/Grade.cshtml", enrollments);
    }
}