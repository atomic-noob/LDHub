using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;
using LDHub.Helpers;

namespace LDHub.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly LoginThrottle _throttle;

    public HomeController(AppDbContext db, LoginThrottle throttle)
    {
        _db = db;
        _throttle = throttle;
    }

    // ── GET: / — public homepage ──────────────────────────────────
  public async Task<IActionResult> Index()
{
    var now = DateTime.Now;
 
    // Load active free trainings
    var freeTrainings = await _db.FreeTrainings
        .AsNoTracking()
        .Include(t => t.Region)
        .Where(t => t.IsActive && t.EndDate >= now)
        .OrderBy(t => t.StartDate)
        .ToListAsync();
 
    ViewBag.FreeTrainings = freeTrainings;
 
    // Load active FAQs ordered by display_order
    var faqs = await _db.Faqs
        .AsNoTracking()
        .Where(f => f.IsActive)
        .OrderBy(f => f.DisplayOrder)
        .ThenBy(f => f.CreatedAt)
        .ToListAsync();
 
    ViewBag.Faqs = faqs;
 
    return View();
}
 
 
    // ── GET: /Home/Login — show login form ────────────────────────
    public IActionResult Login()
    {
        // If already logged in redirect to dashboard
        var role = HttpContext.Session.GetString("UserRole");

        if (role != null)
        {
            return role switch
            {
                "Employee" =>
                    RedirectToAction("Dashboard", "Employee"),

                "ResourcePerson" =>
                    RedirectToAction("Index", "ResourcePerson"),

                "CourseManager" =>
                    RedirectToAction("Dashboard", "CourseManager"),

                "Raam" =>
                    RedirectToAction("Dashboard", "Raam"),

                "Admin" =>
                    RedirectToAction("Dashboard", "Admin"),

                _ =>
                    RedirectToAction("Index", "Home")
            };
        }

        return View(new LoginViewModel());
    }

    
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Login(LoginViewModel model)
{
    if (!ModelState.IsValid)
    {
        return View(model);
    }

    // Too many wrong passwords recently? Stop here.
    var throttleKey = LoginThrottle.Key(HttpContext, model.Email);
    if (_throttle.IsLocked(throttleKey, out var wait))
    {
        ViewBag.Error = $"Too many failed attempts. Please try again in {Math.Ceiling(wait.TotalMinutes)} minute(s).";
        return View(model);
    }
 
    // Find active user by email
    var user = await _db.Users
        .Include(u => u.UserRoles)
        .FirstOrDefaultAsync(u =>
            u.Email == model.Email &&
            u.Status == "Active");
 
    // Check if user exists
    if (user == null)
    {
        _throttle.RegisterFailure(throttleKey);
        ViewBag.Error = "Invalid email or password.";
        return View(model);
    }
 
    // Check password
    if (!BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
    {
        _throttle.RegisterFailure(throttleKey);
        ViewBag.Error = "Invalid email or password.";
        return View(model);
    }
 
    // Block admin from using this login page
    if (user.UserRoles.Any(r => r.Role == "Admin"))
    {
        _throttle.RegisterFailure(throttleKey);
        ViewBag.Error = "Invalid email or password.";
        return View(model);
    }
 
    _throttle.Reset(throttleKey);

    // ── SET BASIC SESSION ──────────────────────────────────────
    HttpContext.Session.SetString("UserId",    user.UserId.ToString());
    HttpContext.Session.SetString("UserName",  $"{user.FirstName} {user.LastName}");
    HttpContext.Session.SetString("UserPhoto", user.Photo ?? "");
 
    // ── STORE ALL USER ROLES ───────────────────────────────────
    var roles = user.UserRoles.Select(r => r.Role).ToList();
    HttpContext.Session.SetString("UserRoles", string.Join(",", roles));
    HttpContext.Session.SetString("DefaultRole", user.DefaultRole ?? "");
 
    // ── EXTERNAL RESOURCE PERSON ───────────────────────────────
    if (user.IsExternal && roles.Contains("ResourcePerson"))
    {
        HttpContext.Session.SetString("UserRole", "ResourcePerson");
        return RedirectToAction("Index", "ResourcePerson");
    }
 
    // ── DEFAULT ROLE REDIRECT ──────────────────────────────────
    if (!string.IsNullOrEmpty(user.DefaultRole) && roles.Contains(user.DefaultRole))
    {
        HttpContext.Session.SetString("UserRole", user.DefaultRole);
 
        return user.DefaultRole switch
        {
            "CourseManager"  => RedirectToAction("Dashboard", "CourseManager"),
            "Raam"           => RedirectToAction("Dashboard", "Raam"),
            "ResourcePerson" => RedirectToAction("Index", "ResourcePerson"),
            _                => RedirectToAction("Dashboard", "Employee")
        };
    }
 
    // ── DEFAULT: EMPLOYEE DASHBOARD ───────────────────────────
    HttpContext.Session.SetString("UserRole", "Employee");
    return RedirectToAction("Dashboard", "Employee");
}


    // ── POST: /Home/SwitchRole — switch active role ───────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SwitchRole(string role)
    {
        var rolesString =
            HttpContext.Session.GetString("UserRoles");

        if (string.IsNullOrEmpty(rolesString))
        {
            return RedirectToAction("Login");
        }

        var roles = rolesString
            .Split(',')
            .ToList();

        // Make sure user actually has the selected role
        if (!roles.Contains(role))
        {
            return RedirectToAction("Login");
        }

        // Set active role
        HttpContext.Session.SetString(
            "UserRole",
            role);

        // Redirect to correct dashboard
        return role switch
        {
            "Employee" =>
                RedirectToAction(
                    "Dashboard",
                    "Employee"),

            "ResourcePerson" =>
                RedirectToAction(
                    "Index",
                    "ResourcePerson"),

            "CourseManager" =>
                RedirectToAction(
                    "Dashboard",
                    "CourseManager"),

            "Raam" =>
                RedirectToAction(
                    "Dashboard",
                    "Raam"),

            _ =>
                RedirectToAction(
                    "Login",
                    "Home")
        };
    }

    // ── GET: /Home/Logout ─────────────────────────────────────────
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();

        return RedirectToAction(
            "Index",
            "Home");
    }

    // ── Error ─────────────────────────────────────────────────────
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public IActionResult Error()
    {
        return View(
            new ErrorViewModel
            {
                RequestId =
                    Activity.Current?.Id ??
                    HttpContext.TraceIdentifier
            });
    }

            [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefaultRole(string role)
        {
            var str = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(str, out int userId))
                return RedirectToAction("Login");

            var user = await _db.Users.FindAsync(userId);
            if (user != null)
            {
                user.DefaultRole = role;
                await _db.SaveChangesAsync();
                TempData["Success"] = $"{role} set as your default role.";
            }

            return RedirectToAction(HttpContext.Session.GetString("UserRole") switch
            {
                "Admin"          => "Dashboard",
                "CourseManager"  => "Dashboard",
                "Raam"           => "Dashboard",
                "ResourcePerson" => "Index",
                _                => "Dashboard"
            }, HttpContext.Session.GetString("UserRole") switch
            {
                "Admin"          => "Admin",
                "CourseManager"  => "CourseManager",
                "Raam"           => "Raam",
                "ResourcePerson" => "ResourcePerson",
                _                => "Employee"
            });
        }

}