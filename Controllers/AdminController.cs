using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;
using LDHub.Helpers;
using LDHub.Services.Interfaces;
using BCrypt.Net;

namespace LDHub.Controllers;

public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly LoginThrottle _throttle;
    private readonly IAdminDashboardService _dashboard;
    private readonly IUserApprovalService _approvals;

    public AdminController(AppDbContext db, LoginThrottle throttle,
        IAdminDashboardService dashboard, IUserApprovalService approvals)
    {
        _db = db;
        _throttle = throttle;
        _dashboard = dashboard;
        _approvals = approvals;
    }

    // ── Auth check helper ─────────────────────────────────────────
    private bool IsAdmin() =>
        HttpContext.Session.GetString("UserRole") == AppRoles.Admin;

    private void SetViewBag()
    {
        ViewBag.ActiveRole = AppRoles.Admin;
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

        // Too many wrong passwords recently? Stop here.
        var throttleKey = LoginThrottle.Key(HttpContext, model.Email);
        if (_throttle.IsLocked(throttleKey, out var wait))
        {
            ViewBag.Error = $"Too many failed attempts. Please try again in {Math.Ceiling(wait.TotalMinutes)} minute(s).";
            return View("~/Views/Admin/Index.cshtml", model);
        }

        // Find user by email
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == model.Email);

        // Check if user exists and is Admin
        if (user == null || !user.UserRoles.Any(r => r.Role == AppRoles.Admin))
        {
            _throttle.RegisterFailure(throttleKey);
            ViewBag.Error = "Invalid email or password.";
            return View("~/Views/Admin/Index.cshtml", model);
        }

        // Verify password
       if (!BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
{
    _throttle.RegisterFailure(throttleKey);
            ViewBag.Error = "Invalid email or password.";
    return View("~/Views/Admin/Index.cshtml", model);
}

        _throttle.Reset(throttleKey);

        // Set session
        HttpContext.Session.SetString("UserRole", AppRoles.Admin);
        HttpContext.Session.SetString("UserId", user.UserId.ToString());
        HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
        HttpContext.Session.SetString("UserPhoto", user.Photo ?? "");

        return RedirectToAction("Dashboard", "Admin");
    }


    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Admin");
    }

    // ── Dashboard ─────────────────────────────────────────────────
   [RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
   public async Task<IActionResult> Dashboard()
{
    SetViewBag();

    var now = DateTime.Now;

    var d = await _dashboard.GetAsync(now);

    ViewBag.TotalEmployees     = d.TotalEmployees;
    ViewBag.TotalTrainings     = d.TotalTrainings;
    ViewBag.ActiveTrainings    = d.ActiveTrainings;
    ViewBag.CompletedTrainings = d.CompletedTrainings;
    ViewBag.PendingCount       = d.PendingCount;
    ViewBag.TotalSME           = d.TotalSME;
    ViewBag.TotalUsers         = d.TotalUsers;
    ViewBag.PassingRate        = d.PassingRate;
    ViewBag.PassedCount        = d.PassedCount;
    ViewBag.FailedCount        = d.FailedCount;
    ViewBag.TotalAttempts      = d.TotalAttempts;

    // Chart data as JSON
    ViewBag.MonthlyTrainings      = System.Text.Json.JsonSerializer.Serialize(d.MonthlyTrainings);
    ViewBag.MonthlyRegistrations  = System.Text.Json.JsonSerializer.Serialize(d.MonthlyRegistrations);
    ViewBag.RegionLabels          = System.Text.Json.JsonSerializer.Serialize(d.RegionLabels);
    ViewBag.RegionCounts          = System.Text.Json.JsonSerializer.Serialize(d.RegionCounts);
    ViewBag.ScheduledCount        = d.ScheduledCount;
    ViewBag.OngoingCount          = d.OngoingCount;
    ViewBag.EndedCount            = d.EndedCount;

    ViewBag.RecentTrainings = d.RecentTrainings;
    ViewBag.RecentPending   = d.RecentPending;

    return View("~/Views/Admin/Dashboard.cshtml");
}

    // ── Manage Users ──────────────────────────────────────────────
[RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
public async Task<IActionResult> Employees()
{
    SetViewBag();

    var users = await _db.Users
        .Include(u => u.UserRoles)
        .Include(u => u.Region)
        .Include(u => u.Office)
        .Include(u => u.Division)
        // ← Exclude Admin accounts
        .Where(u => !u.UserRoles.Any(r => r.Role == AppRoles.Admin))
        .OrderBy(u => u.LastName)
        .ToListAsync();

    return View("~/Views/Admin/Employees.cshtml", users);
}

// POST: /Admin/EmployeeEdit
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
public async Task<IActionResult> EmployeeEdit(User model)
{

    var user = await _db.Users.FindAsync(model.UserId);
    if (user == null)
    {
        TempData["Error"] = "Employee not found!";
        return RedirectToAction("Employees");
    }

    user.FirstName     = model.FirstName;
    user.MiddleName    = model.MiddleName;
    user.LastName      = model.LastName;
    user.ContactNumber = model.ContactNumber;
    user.Position      = model.Position;
    user.Designation   = model.Designation;
    user.SalaryGrade   = model.SalaryGrade;
    user.RegionId      = model.RegionId;
    user.OfficeId      = model.OfficeId;
    user.DivisionId    = model.DivisionId;
    user.UpdatedAt     = DateTime.UtcNow;

    await _db.SaveChangesAsync();
    TempData["Success"] = "Employee updated successfully!";
    return RedirectToAction("Employees");
}

// POST: /Admin/ToggleEmployeeStatus
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
public async Task<IActionResult> ToggleEmployeeStatus(int id)
{

    var user = await _db.Users.FindAsync(id);
    if (user == null)
    {
        TempData["Error"] = "Employee not found!";
        return RedirectToAction("Employees");
    }

    user.Status    = user.Status == "Active" ? "Inactive" : "Active";
    user.UpdatedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();

    TempData["Success"] = $"{user.FirstName} {user.LastName} is now {user.Status}!";
    return RedirectToAction("Employees");
}

// POST: /Admin/AssignRole
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
public async Task<IActionResult> AssignRole(int userId, string role)
{

    // Check if role already assigned
    var exists = await _db.UserRoles
        .AnyAsync(r => r.UserId == userId && r.Role == role);

    if (exists)
    {
        TempData["Error"] = "Role already assigned!";
        return RedirectToAction("Employees");
    }

    // Rule: only ONE additional role besides "Employee" is allowed
    // (Raam, CourseManager, ResourcePerson are mutually exclusive)
    if (role != AppRoles.Employee)
    {
        var hasOtherSpecialRole = await _db.UserRoles
            .AnyAsync(r => r.UserId == userId
                        && r.Role != AppRoles.Employee
                        && r.Role != AppRoles.Admin);

        if (hasOtherSpecialRole)
        {
            TempData["Error"] = "This employee already has a special role assigned. " +
                                 "Remove the existing role first before assigning a new one.";
            return RedirectToAction("Employees");
        }
    }

    _db.UserRoles.Add(new UserRole
    {
        UserId     = userId,
        Role       = role,
        AssignedAt = DateTime.UtcNow
    });

    await _db.SaveChangesAsync();
    TempData["Success"] = $"Role '{role}' assigned successfully!";
    return RedirectToAction("Employees");
}

// POST: /Admin/RemoveRole
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
public async Task<IActionResult> RemoveRole(int userId, string role)
{

    var userRole = await _db.UserRoles
        .FirstOrDefaultAsync(r => r.UserId == userId && r.Role == role);

    if (userRole == null)
    {
        TempData["Error"] = "Role not found!";
        return RedirectToAction("Employees");
    }

    // Prevent removing last role
    var roleCount = await _db.UserRoles
        .CountAsync(r => r.UserId == userId);

    if (roleCount <= 1)
    {
        TempData["Error"] = "Cannot remove the last role!";
        return RedirectToAction("Employees");
    }

    _db.UserRoles.Remove(userRole);
    await _db.SaveChangesAsync();
    TempData["Success"] = $"Role '{role}' removed successfully!";
    return RedirectToAction("Employees");
}

    [RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
    public async Task<IActionResult> PendingEmployees()
        {
            SetViewBag();

            var pending = await _db.PendingParticipants
                .Include(p => p.Region)
                .Include(p => p.Office)
                .Include(p => p.Division)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View("~/Views/Admin/PendingEmployees.cshtml", pending);
        }

    // POST: /Admin/ApproveEmployee
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
public async Task<IActionResult> ApproveEmployee(int id)
{

    var pending = await _db.PendingParticipants
        .FirstOrDefaultAsync(p => p.ParticipantId == id);

    if (pending == null)
    {
        TempData["Error"] = "Pending employee not found!";
        return RedirectToAction("PendingEmployees");
    }

    var error = await _approvals.ApproveAsync(pending, removePending: true);
    if (error != null)
    {
        TempData["Error"] = error;
        return RedirectToAction("PendingEmployees");
    }

    TempData["Success"] = $"{pending.FirstName} {pending.LastName} has been approved!";
    return RedirectToAction("PendingEmployees");
}

// POST: /Admin/RejectEmployee
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
public async Task<IActionResult> RejectEmployee(int id)
{

    var pending = await _db.PendingParticipants
        .FirstOrDefaultAsync(p => p.ParticipantId == id);

    if (pending == null)
    {
        TempData["Error"] = "Pending employee not found!";
        return RedirectToAction("PendingEmployees");
    }

    _db.PendingParticipants.Remove(pending);
    await _db.SaveChangesAsync();

    TempData["Success"] = $"{pending.FirstName} {pending.LastName} has been rejected.";
    return RedirectToAction("PendingEmployees");
}
// ── GET: /Admin/ResourcePersons ───────────────────────────────
[RequireRole(AppRoles.Admin)]
public async Task<IActionResult> ResourcePersons()
{
    SetViewBag();
 
    var resourcePersons = await _db.Users
        .Include(u => u.Region)
        .Include(u => u.Office)
        .Include(u => u.Division)
        .Where(u => u.IsExternal == true)
        .OrderByDescending(u => u.CreatedAt)
        .ToListAsync();
 
    return View("~/Views/Admin/ResourcePersons.cshtml", resourcePersons);
}
 
// ── POST: /Admin/CreateResourcePerson ────────────────────────
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin)]
public async Task<IActionResult> CreateResourcePerson(
    string firstName, string lastName, string? middleName,
    string email, string password, string? organization,
    string? expertise, string? contactNumber, string? gender,
    int? regionId, int? officeId, int? divisionId)
{
 
    // Check email exists
    var emailExists = await _db.Users.AnyAsync(u => u.Email == email);
    if (emailExists)
    {
        TempData["Error"] = "Email already exists.";
        return RedirectToAction("ResourcePersons");
    }
 
    var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
 
    var user = new User
    {
        FirstName     = firstName,
        MiddleName    = middleName,
        LastName      = lastName,
        Email         = email,
        Password      = hashedPassword,
        Organization  = organization,
        Expertise     = expertise,
        ContactNumber = contactNumber,
        Gender        = gender,
        RegionId      = regionId,
        OfficeId      = officeId,
        DivisionId    = divisionId,
        IsExternal    = true,
        Status = "Active",
        CreatedAt     = DateTime.UtcNow,
        UpdatedAt     = DateTime.UtcNow
    };
 
    _db.Users.Add(user);
    await _db.SaveChangesAsync();
 
    // Assign ResourcePerson role
    _db.UserRoles.Add(new UserRole
    {
        UserId   = user.UserId,
        Role = AppRoles.ResourcePerson
    });
    await _db.SaveChangesAsync();
 
    TempData["Success"] = $"Resource Person {firstName} {lastName} created successfully!";
    return RedirectToAction("ResourcePersons");
}
 
// ── POST: /Admin/EditResourcePerson ──────────────────────────
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin)]
public async Task<IActionResult> EditResourcePerson(
    int userId, string firstName, string lastName, string? middleName,
    string email, string? newPassword, string? organization,
    string? expertise, string? contactNumber, string? gender,
    int? regionId, int? officeId, int? divisionId)
{
 
    var user = await _db.Users.FindAsync(userId);
    if (user == null)
    {
        TempData["Error"] = "User not found.";
        return RedirectToAction("ResourcePersons");
    }
 
    user.FirstName     = firstName;
    user.MiddleName    = middleName;
    user.LastName      = lastName;
    user.Email         = email;
    user.Organization  = organization;
    user.Expertise     = expertise;
    user.ContactNumber = contactNumber;
    user.Gender        = gender;
    user.RegionId      = regionId;
    user.OfficeId      = officeId;
    user.DivisionId    = divisionId;
    user.UpdatedAt     = DateTime.UtcNow;
 
    // Only update password if new one provided
    if (!string.IsNullOrWhiteSpace(newPassword))
        user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
 
    await _db.SaveChangesAsync();
 
    TempData["Success"] = "Resource Person updated successfully!";
    return RedirectToAction("ResourcePersons");
}
 
// ── POST: /Admin/ToggleResourcePersonStatus ───────────────────
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin)]
public async Task<IActionResult> ToggleResourcePersonStatus(int userId)
{

    var user = await _db.Users.FindAsync(userId);
    if (user == null)
    {
        TempData["Error"] = "User not found.";
        return RedirectToAction("ResourcePersons");
    }

    user.Status    = user.Status == "Active" ? "Inactive" : "Active";
    user.UpdatedAt = DateTime.UtcNow;

    await _db.SaveChangesAsync();

    TempData["Success"] = $"{user.FirstName} {user.LastName} has been {user.Status.ToLower()}.";
    return RedirectToAction("ResourcePersons");
}




    // ── Trainings ─────────────────────────────────────────────────
    [RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
    public IActionResult Trainings()
    {
        SetViewBag();
        return View("~/Views/Admin/Trainings.cshtml");
    }

    [RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
    public IActionResult FreeTrainings()
    {
        SetViewBag();
        return View("~/Views/Admin/FreeTrainings.cshtml");
    }

    // ── Announcements ─────────────────────────────────────────────
    [RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
    public IActionResult Announcements()
    {
        SetViewBag();
        return View("~/Views/Admin/Announcements.cshtml");
    }

    // ── Reports ───────────────────────────────────────────────────
    [RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
    public IActionResult ReportUsers()
    {
        SetViewBag();
        return View("~/Views/Admin/ReportUsers.cshtml");
    }

    [RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
    public IActionResult ReportIdp()
    {
        SetViewBag();
        return View("~/Views/Admin/ReportIdp.cshtml");
    }

    [RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
    public IActionResult ReportTraining()
    {
        SetViewBag();
        return View("~/Views/Admin/ReportTraining.cshtml");
    }

    // ── FAQs ──────────────────────────────────────────────────────
    [RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
    public IActionResult Faqs()
    {
        SetViewBag();
        return View("~/Views/Admin/Faqs.cshtml");
    }
    [HttpGet]
public async Task<IActionResult> GetDivisionsByOffice(int officeId)
{
    var divisions = await _db.Divisions
        .Where(d => d.OfficeId == officeId)
        .OrderBy(d => d.DivisionName)
        .Select(d => new { d.DivisionId, d.DivisionName })
        .ToListAsync();
    return Json(divisions);
}

// ── GET: /Admin/AccountSettings ──────────────────────────────
[RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
public async Task<IActionResult> AccountSettings()
{
    SetViewBag();

    var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
    var admin  = await _db.Users.FindAsync(userId);

    return View("~/Views/Admin/AccountSettings.cshtml", admin);
}

// ── POST: /Admin/UpdateAdminAccount ──────────────────────────
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Admin, RedirectAction = "Index", RedirectController = "Admin")]
public async Task<IActionResult> UpdateAdminAccount(string email, 
    string? newPassword, string currentPassword)
{

    var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
    var admin  = await _db.Users.FindAsync(userId);

    if (admin == null) return RedirectToAction("Index");

    // Verify current password first
    if (!BCrypt.Net.BCrypt.Verify(currentPassword, admin.Password))
    {
        TempData["Error"] = "Current password is incorrect!";
        return RedirectToAction("AccountSettings");
    }

    // Update email
    admin.Email     = email;
    admin.UpdatedAt = DateTime.UtcNow;

    // Update password only if provided
    if (!string.IsNullOrEmpty(newPassword))
    {
        admin.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
    }

    await _db.SaveChangesAsync();
    HttpContext.Session.SetString("UserName", $"{admin.FirstName} {admin.LastName}");
    TempData["Success"] = "Admin account updated successfully!";
    return RedirectToAction("AccountSettings");
}


}