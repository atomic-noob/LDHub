using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;

namespace LDHub.Controllers;

public class EmployeeController : Controller
{
    // ── Database connection ───────────────────────────────────────
    // AppDbContext is injected here automatically by ASP.NET Core
    // This is how we talk to the database — like $conn in PHP
    private readonly AppDbContext _db;

    public EmployeeController(AppDbContext db)
    {
        _db = db;
    }

    // ── Auth check helper ─────────────────────────────────────────
    // Checks if the current session has Employee role
    // Used to protect pages from unauthorized access
    // Equivalent to checking $_SESSION['role'] == 'Employee' in PHP
    private bool IsEmployee() =>
        HttpContext.Session.GetString("UserRole") == "Employee";

    // ── ViewBag helper ────────────────────────────────────────────
    // Sets common data needed by the layout and sidebar
    // ViewBag is like passing variables to the view
    // Equivalent to $data['userName'] = $_SESSION['name'] in PHP
    private void SetViewBag()
    {
        ViewBag.ActiveRole = "Employee";
        ViewBag.UserName  = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto = HttpContext.Session.GetString("UserPhoto");
    }

    // ── GET: /Employee/Dashboard ──────────────────────────────────
    // Main landing page after employee logs in
    // Shows overview of trainings, progress, announcements
    // If not logged in as Employee → redirect to login page
    public IActionResult Dashboard()
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Employee/Dashboard.cshtml");
    }

    // ── GET: /Employee/Profile ────────────────────────────────────
    // Shows the employee's personal information
    // Name, email, position, region, office, division, photo
    // If not logged in as Employee → redirect to login page
    public IActionResult Profile()
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Employee/Profile.cshtml");
    }

    // ── GET: /Employee/Grade ──────────────────────────────────────
    // Shows the employee's training grades and scores
    // Lists all trainings they completed with their results
    // If not logged in as Employee → redirect to login page
    public IActionResult Grade()
    {
        if (!IsEmployee()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Employee/Grade.cshtml");
    }
}