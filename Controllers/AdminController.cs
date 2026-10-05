using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;using BCrypt.Net;

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
   public async Task<IActionResult> Dashboard()
{
    if (!IsAdmin()) return RedirectToAction("Index");
    SetViewBag();

    var now = DateTime.Now;

    // ── KPI Stats ──────────────────────────────────────────────
    var totalEmployees  = await _db.Users
        .Include(u => u.UserRoles)
        .Where(u => u.UserRoles.Any(r => r.Role == "Employee") && u.IsExternal == false)
        .CountAsync();

    var totalTrainings  = await _db.Trainings.CountAsync();

    var activeTrainings = await _db.Trainings
        .Where(t => t.StartDate <= now && t.EndDate >= now)
        .CountAsync();

    var completedTrainings = await _db.Trainings
        .Where(t => t.EndDate < now)
        .CountAsync();

    var pendingCount = await _db.PendingParticipants
        .Where(p => p.Status == "Inactive")
        .CountAsync();

    var totalSME = await _db.Users
        .Include(u => u.UserRoles)
        .Where(u => u.UserRoles.Any(r => r.Role == "ResourcePerson") && u.IsExternal == true)
        .CountAsync();

    var totalUsers = await _db.Users.CountAsync();

    // ── Passing Rate ───────────────────────────────────────────
    var allAttempts = await _db.ParticipantTestAttempts
        .Where(a => a.TestType == "PostTest")
        .ToListAsync();

    var passedCount = allAttempts.Count(a => a.IsPassed);
    var passingRate = allAttempts.Count > 0
        ? Math.Round((double)passedCount / allAttempts.Count * 100, 1)
        : 0;

    // ── Monthly Trainings (Bar Chart) ──────────────────────────
    var allTrainings = await _db.Trainings
        .Include(t => t.TrainingParticipants)
        .Include(t => t.Region)
        .OrderByDescending(t => t.CreatedAt)
        .ToListAsync();

    var monthlyTrainings = Enumerable.Range(1, 12)
        .Select(m => allTrainings.Count(t => t.CreatedAt.Month == m && t.CreatedAt.Year == now.Year))
        .ToList();

    // ── Training Status (Pie Chart) ────────────────────────────
    var scheduledCount = allTrainings.Count(t => t.StartDate.HasValue && t.StartDate > now);
    var ongoingCount   = allTrainings.Count(t => t.StartDate <= now && t.EndDate >= now);
    var endedCount     = allTrainings.Count(t => t.EndDate.HasValue && t.EndDate < now);

    // ── Participants per Region (Bar Chart) ────────────────────
    var regions = await _db.Regions.ToListAsync();
    var participantsByRegion = regions.Select(r => new
    {
        RegionName = r.RegionName,
        Count      = _db.Users.Count(u => u.RegionId == r.RegionId
                        && u.UserRoles.Any(ur => ur.Role == "Employee"))
    }).OrderByDescending(r => r.Count).Take(10).ToList();

    // ── Employee Registrations per Month (Line Chart) ──────────
    var allUsers = await _db.Users.ToListAsync();
    var monthlyRegistrations = Enumerable.Range(1, 12)
        .Select(m => allUsers.Count(u => u.CreatedAt.Month == m && u.CreatedAt.Year == now.Year))
        .ToList();

    // ── Pass vs Fail (Pie Chart) ───────────────────────────────
    var failedCount2 = allAttempts.Count(a => !a.IsPassed);

    // ── Recent Trainings ───────────────────────────────────────
    var recentTrainings = allTrainings.Take(5).ToList();

    // ── Recent Pending ─────────────────────────────────────────
    var recentPending = await _db.PendingParticipants
        .Where(p => p.Status == "Inactive")
        .OrderByDescending(p => p.CreatedAt)
        .Take(5)
        .ToListAsync();

    // ── Pass to ViewBag ────────────────────────────────────────
    ViewBag.TotalEmployees     = totalEmployees;
    ViewBag.TotalTrainings     = totalTrainings;
    ViewBag.ActiveTrainings    = activeTrainings;
    ViewBag.CompletedTrainings = completedTrainings;
    ViewBag.PendingCount       = pendingCount;
    ViewBag.TotalSME           = totalSME;
    ViewBag.TotalUsers         = totalUsers;
    ViewBag.PassingRate        = passingRate;
    ViewBag.PassedCount        = passedCount;
    ViewBag.FailedCount        = failedCount2;
    ViewBag.TotalAttempts      = allAttempts.Count;

    // Chart data as JSON
    ViewBag.MonthlyTrainings      = System.Text.Json.JsonSerializer.Serialize(monthlyTrainings);
    ViewBag.MonthlyRegistrations  = System.Text.Json.JsonSerializer.Serialize(monthlyRegistrations);
    ViewBag.RegionLabels          = System.Text.Json.JsonSerializer.Serialize(participantsByRegion.Select(r => r.RegionName).ToList());
    ViewBag.RegionCounts          = System.Text.Json.JsonSerializer.Serialize(participantsByRegion.Select(r => r.Count).ToList());
    ViewBag.ScheduledCount        = scheduledCount;
    ViewBag.OngoingCount          = ongoingCount;
    ViewBag.EndedCount            = endedCount;

    ViewBag.RecentTrainings = recentTrainings;
    ViewBag.RecentPending   = recentPending;

    return View("~/Views/Admin/Dashboard.cshtml");
}
    public IActionResult Hash()
{
    var hash = BCrypt.Net.BCrypt.HashPassword("Password@123");
    return Content(hash);
}

    // ── Manage Users ──────────────────────────────────────────────
public async Task<IActionResult> Employees()
{
    if (!IsAdmin()) return RedirectToAction("Index");
    SetViewBag();

    var users = await _db.Users
        .Include(u => u.UserRoles)
        .Include(u => u.Region)
        .Include(u => u.Office)
        .Include(u => u.Division)
        // ← Exclude Admin accounts
        .Where(u => !u.UserRoles.Any(r => r.Role == "Admin"))
        .OrderBy(u => u.LastName)
        .ToListAsync();

    return View("~/Views/Admin/Employees.cshtml", users);
}

// POST: /Admin/EmployeeEdit
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EmployeeEdit(User model)
{
    if (!IsAdmin()) return RedirectToAction("Index");

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
public async Task<IActionResult> ToggleEmployeeStatus(int id)
{
    if (!IsAdmin()) return RedirectToAction("Index");

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
public async Task<IActionResult> AssignRole(int userId, string role)
{
    if (!IsAdmin()) return RedirectToAction("Index");

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
    if (role != "Employee")
    {
        var hasOtherSpecialRole = await _db.UserRoles
            .AnyAsync(r => r.UserId == userId
                        && r.Role != "Employee"
                        && r.Role != "Admin");

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
public async Task<IActionResult> RemoveRole(int userId, string role)
{
    if (!IsAdmin()) return RedirectToAction("Index");

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

    public async Task<IActionResult> PendingEmployees()
        {
            if (!IsAdmin()) return RedirectToAction("Index");
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
public async Task<IActionResult> ApproveEmployee(int id)
{
    if (!IsAdmin()) return RedirectToAction("Index");

    var pending = await _db.PendingParticipants
        .FirstOrDefaultAsync(p => p.ParticipantId == id);

    if (pending == null)
    {
        TempData["Error"] = "Pending employee not found!";
        return RedirectToAction("PendingEmployees");
    }

    // Check if email already exists in users
    var exists = await _db.Users.AnyAsync(u => u.Email == pending.Email);
    if (exists)
    {
        TempData["Error"] = "Email already exists in users!";
        return RedirectToAction("PendingEmployees");
    }

    // Copy to users table
    var user = new User
    {
        FirstName     = pending.FirstName,
        MiddleName    = pending.MiddleName,
        LastName      = pending.LastName,
        Email         = pending.Email,
        Password      = pending.Password,
        ContactNumber = pending.ContactNumber,
        Gender        = pending.Gender,
        Age           = pending.Age,
        Position      = pending.Position,
        Designation   = pending.Designation,
        SalaryGrade   = pending.SalaryGrade,
        RegionId      = pending.RegionId,
        OfficeId      = pending.OfficeId,
        DivisionId    = pending.DivisionId,
        Status        = "Active",
        CreatedAt     = DateTime.UtcNow
    };

    _db.Users.Add(user);
    await _db.SaveChangesAsync();

    // Assign Employee role
    _db.UserRoles.Add(new UserRole
    {
        UserId     = user.UserId,
        Role       = "Employee",
        AssignedAt = DateTime.UtcNow
    });

    // Remove from pending
    _db.PendingParticipants.Remove(pending);
    await _db.SaveChangesAsync();

    TempData["Success"] = $"{pending.FirstName} {pending.LastName} has been approved!";
    return RedirectToAction("PendingEmployees");
}

// POST: /Admin/RejectEmployee
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RejectEmployee(int id)
{
    if (!IsAdmin()) return RedirectToAction("Index");

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
public async Task<IActionResult> ResourcePersons()
{
    if (!IsAdmin()) return RedirectToAction("Login", "Home");
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
public async Task<IActionResult> CreateResourcePerson(
    string firstName, string lastName, string? middleName,
    string email, string password, string? organization,
    string? expertise, string? contactNumber, string? gender,
    int? regionId, int? officeId, int? divisionId)
{
    if (!IsAdmin()) return RedirectToAction("Login", "Home");
 
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
        Role = "ResourcePerson"
    });
    await _db.SaveChangesAsync();
 
    TempData["Success"] = $"Resource Person {firstName} {lastName} created successfully!";
    return RedirectToAction("ResourcePersons");
}
 
// ── POST: /Admin/EditResourcePerson ──────────────────────────
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EditResourcePerson(
    int userId, string firstName, string lastName, string? middleName,
    string email, string? newPassword, string? organization,
    string? expertise, string? contactNumber, string? gender,
    int? regionId, int? officeId, int? divisionId)
{
    if (!IsAdmin()) return RedirectToAction("Login", "Home");
 
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
public async Task<IActionResult> ToggleResourcePersonStatus(int userId)
{
    if (!IsAdmin()) return RedirectToAction("Login", "Home");

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
public async Task<IActionResult> AccountSettings()
{
    if (!IsAdmin()) return RedirectToAction("Index");
    SetViewBag();

    var userId = int.Parse(HttpContext.Session.GetString("UserId") ?? "0");
    var admin  = await _db.Users.FindAsync(userId);

    return View("~/Views/Admin/AccountSettings.cshtml", admin);
}

// ── POST: /Admin/UpdateAdminAccount ──────────────────────────
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UpdateAdminAccount(string email, 
    string? newPassword, string currentPassword)
{
    if (!IsAdmin()) return RedirectToAction("Index");

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