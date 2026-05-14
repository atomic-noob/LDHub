using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;

namespace LDHub.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }

    // ── GET: / — public homepage ──────────────────────────────────
    public IActionResult Index()
    {
        return View();
    }

    // ── GET: /Home/Login — show login form ────────────────────────
    public IActionResult Login()
    {
        // If already logged in redirect to dashboard
        var role = HttpContext.Session.GetString("UserRole");
        if (role != null) return RedirectToAction("Index");

        return View(new LoginViewModel());
    }

    // ── POST: /Home/Login — process login ─────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Find user by email
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == model.Email && u.Status == "Active");

        // Check if user exists and password is correct
        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
        {
            ViewBag.Error = "Invalid email or password.";
            return View(model);
        }

        // Block admin from using this login page
        if (user.UserRoles.Any(r => r.Role == "Admin"))
        {
            ViewBag.Error = "Invalid email or password.";
            return View(model);
        }

        // Set session
        HttpContext.Session.SetString("UserId",    user.UserId.ToString());
        HttpContext.Session.SetString("UserName",  $"{user.FirstName} {user.LastName}");
        HttpContext.Session.SetString("UserPhoto", user.Photo ?? "");

        // Store ALL roles in session for navbar role switcher
        var roles = user.UserRoles.Select(r => r.Role).ToList();
        HttpContext.Session.SetString("UserRoles", string.Join(",", roles));

        // Redirect based on is_external flag
        // External (outsider SME) → ResourcePerson dashboard
        if (user.IsExternal && roles.Contains("ResourcePerson"))
        {
            HttpContext.Session.SetString("UserRole", "ResourcePerson");
            return RedirectToAction("Index", "ResourcePerson");
        }

        // Internal DENR staff → always Employee dashboard as default
        // Role switcher in navbar handles switching to CourseManager/RAAM
        HttpContext.Session.SetString("UserRole", "Employee");
        return RedirectToAction("Dashboard", "Employee");
    }

    // ── POST: /Home/SwitchRole — switch active role ───────────────
    [HttpPost]
    public IActionResult SwitchRole(string role)
    {
        var rolesString = HttpContext.Session.GetString("UserRoles");
        if (string.IsNullOrEmpty(rolesString))
            return RedirectToAction("Login");

        var roles = rolesString.Split(',').ToList();

        // Make sure they actually have this role
        if (!roles.Contains(role))
            return RedirectToAction("Login");

        HttpContext.Session.SetString("UserRole", role);

        // Redirect to correct dashboard
        return role switch
        {
            "Employee"       => RedirectToAction("Dashboard", "Employee"),
            "ResourcePerson" => RedirectToAction("Index",     "ResourcePerson"),
            "CourseManager"  => RedirectToAction("Dashboard", "CourseManager"),
            "Raam"           => RedirectToAction("Dashboard", "Raam"),
            _                => RedirectToAction("Login")
        };
    }

    // ── GET: /Home/Logout ─────────────────────────────────────────
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index");
    }

    // ── Error ─────────────────────────────────────────────────────
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { 
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier 
        });
    }
}