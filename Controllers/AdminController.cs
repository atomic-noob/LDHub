using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;

namespace LDHub.Controllers;

public class AdminController : Controller
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    // ── Auth check helper ─────────────────────────────────────────
    private bool IsAdmin() =>
        HttpContext.Session.GetString("UserRole") == "Admin";

    private void SetViewBag()
    {
        ViewBag.ActiveRole = "Admin";
        ViewBag.UserName = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto = HttpContext.Session.GetString("UserPhoto");
    }

    // ── Login ─────────────────────────────────────────────────────
    public IActionResult Index()
    {
        return View("~/Views/Admin/Index.cshtml");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View("~/Views/Admin/Index.cshtml", model);

        // Find user by email
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == model.Email);

        // Check if user exists and is Admin
        if (user == null || !user.UserRoles.Any(r => r.Role == "Admin"))
        {
            ViewBag.Error = "Invalid email or password.";
            return View("~/Views/Admin/Index.cshtml", model);
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
        {
            ViewBag.Error = "Invalid email or password.";
            return View("~/Views/Admin/Index.cshtml", model);
        }

        // Set session
        HttpContext.Session.SetString("UserRole", "Admin");
        HttpContext.Session.SetString("UserId", user.UserId.ToString());
        HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
        HttpContext.Session.SetString("UserPhoto", user.Photo ?? "");

        return RedirectToAction("Dashboard", "Admin");
    }
    // GET: /Admin/Hash — TEMPORARY, remove after use


    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Admin");
    }

    // ── Dashboard ─────────────────────────────────────────────────
    public IActionResult Dashboard()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/Dashboard.cshtml");
    }
    public IActionResult Hash()
{
    var hash = BCrypt.Net.BCrypt.HashPassword("Password@123");
    return Content(hash);
}

    // ── Manage Users ──────────────────────────────────────────────
    public IActionResult Employees()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/Employees.cshtml");
    }

    public IActionResult PendingEmployees()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/PendingEmployees.cshtml");
    }

    public IActionResult ResourcePersons()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/ResourcePersons.cshtml");
    }

    // ── Locations ─────────────────────────────────────────────────
    public IActionResult Regions()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/Regions.cshtml");
    }

    public IActionResult Offices()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/Offices.cshtml");
    }

    public IActionResult Divisions()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/Divisions.cshtml");
    }

    // ── Trainings ─────────────────────────────────────────────────
    public IActionResult Trainings()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/Trainings.cshtml");
    }

    public IActionResult FreeTrainings()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/FreeTrainings.cshtml");
    }

    // ── Announcements ─────────────────────────────────────────────
    public IActionResult Announcements()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/Announcements.cshtml");
    }

    // ── Reports ───────────────────────────────────────────────────
    public IActionResult ReportUsers()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/ReportUsers.cshtml");
    }

    public IActionResult ReportIdp()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/ReportIdp.cshtml");
    }

    public IActionResult ReportTraining()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/ReportTraining.cshtml");
    }

    // ── FAQs ──────────────────────────────────────────────────────
    public IActionResult Faqs()
    {
        if (!IsAdmin()) return RedirectToAction("Index");
        SetViewBag();
        return View("~/Views/Admin/Faqs.cshtml");
    }
}