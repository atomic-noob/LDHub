using LDHub.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;
using LDHub.Services.Interfaces;

namespace LDHub.Controllers;

public class RaamController : Controller
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly IUserApprovalService _approvals;

    public RaamController(AppDbContext db, IEmailService email, IUserApprovalService approvals)
    {
        _db = db;
        _email = email;
        _approvals = approvals;
    }

    // ── Auth helpers ───────────────────────────────────────────
    private bool IsRaam() =>
        HttpContext.Session.GetString("UserRole") == AppRoles.Raam;

    private int GetUserId()
    {
        var str = HttpContext.Session.GetString("UserId");
        return int.TryParse(str, out int id) ? id : 0;
    }

    private void SetViewBag()
    {
        ViewBag.ActiveRole = AppRoles.Raam;
        ViewBag.UserName   = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto  = HttpContext.Session.GetString("UserPhoto");
    }

    // ── Get RAAM's region ID ───────────────────────────────────
    private async Task<int?> GetRaamRegionId()
    {
        var user = await _db.Users.FindAsync(GetUserId());
        return user?.RegionId;
    }

    // ════════════════════════════════════════════════════════════
    //  DASHBOARD
    // ════════════════════════════════════════════════════════════

    [RequireRole(AppRoles.Raam)]
    public async Task<IActionResult> Dashboard()
    {
        SetViewBag();

        var regionId = await GetRaamRegionId();

        // Region-filtered stats
        var trainings = await _db.Trainings
            .Where(t => t.RegionId == regionId)
            .ToListAsync();

        var trainingIds = trainings.Select(t => t.TrainingId).ToList();

        var totalParticipants = await _db.TrainingParticipants
            .Where(tp => trainingIds.Contains(tp.TrainingId))
            .Select(tp => tp.UserId)
            .Distinct()
            .CountAsync();

        var pendingCount = await _db.PendingParticipants
            .Where(p => p.RegionId == regionId && p.Status == RecordStatus.Inactive)
            .CountAsync();

        var completedCount = await _db.TrainingParticipants
            .Where(tp => trainingIds.Contains(tp.TrainingId)
                      && tp.CompletionStatus == EnrollmentStatus.Completed)
            .CountAsync();

        var ongoingCount = trainings.Count(t =>
            t.StartDate <= DateTime.Now && t.EndDate >= DateTime.Now);

        // Recent trainings in region
        var recentTrainings = trainings
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .ToList();

        // Region info
        var region = await _db.Regions.FindAsync(regionId);

        ViewBag.RegionName        = region?.RegionName ?? "Unknown Region";
        ViewBag.RegionId          = regionId;
        ViewBag.TotalTrainings    = trainings.Count;
        ViewBag.TotalParticipants = totalParticipants;
        ViewBag.PendingCount      = pendingCount;
        ViewBag.CompletedCount    = completedCount;
        ViewBag.OngoingCount      = ongoingCount;
        ViewBag.RecentTrainings   = recentTrainings;

        return View("~/Views/Raam/Dashboard.cshtml");
    }

    // ════════════════════════════════════════════════════════════
    //  TRAINING — region filtered, reuse Training/Index.cshtml
    // ════════════════════════════════════════════════════════════

    [RequireRole(AppRoles.Raam)]
    public async Task<IActionResult> Training()
    {
        SetViewBag();

        var regionId = await GetRaamRegionId();

        var trainings = await _db.Trainings
            .Where(t => t.RegionId == regionId)
            .Include(t => t.TrainingParticipants)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        // Pass flag so Training/Index.cshtml knows it's RAAM context
        ViewBag.IsRaam   = true;
        ViewBag.RegionId = regionId;

        return View("~/Views/Training/Index.cshtml", trainings);
    }

    // ════════════════════════════════════════════════════════════
    //  PENDING PARTICIPANTS — region filtered
    // ════════════════════════════════════════════════════════════

    [RequireRole(AppRoles.Raam)]
    public async Task<IActionResult> PendingParticipants()
    {
        SetViewBag();

        var regionId = await GetRaamRegionId();

        var pending = await _db.PendingParticipants
            .Include(p => p.Region)
            .Include(p => p.Office)
            .Include(p => p.Division)
            .Where(p => p.RegionId == regionId && p.Status == RecordStatus.Inactive)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        ViewBag.RegionId = regionId;

        return View("~/Views/Raam/PendingParticipants.cshtml", pending);
    }

    // ── POST: Approve participant ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole(AppRoles.Raam)]
    public async Task<IActionResult> ApproveParticipant(int pendingId)
    {

        var pending = await _db.PendingParticipants.FindAsync(pendingId);
        if (pending == null)
        {
            TempData["Error"] = "Participant not found.";
            return RedirectToAction("PendingParticipants");
        }

        // A RAAM can only act on registrations from their own region
        var myRegion = await GetRaamRegionId();
        if (myRegion == null || pending.RegionId != myRegion)
        {
            TempData["Error"] = "That registration is outside your region.";
            return RedirectToAction("PendingParticipants");
        }

        var error = await _approvals.ApproveAsync(pending, removePending: false);
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction("PendingParticipants");
        }

        TempData["Success"] = $"{pending.FirstName} {pending.LastName} approved successfully!";
        return RedirectToAction("PendingParticipants");
    }

    // ── POST: Reject participant ───────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole(AppRoles.Raam)]
    public async Task<IActionResult> RejectParticipant(int pendingId, string? reason)
    {

        var pending = await _db.PendingParticipants.FindAsync(pendingId);
        if (pending == null)
        {
            TempData["Error"] = "Participant not found.";
            return RedirectToAction("PendingParticipants");
        }

        // A RAAM can only act on registrations from their own region
        var myRegion = await GetRaamRegionId();
        if (myRegion == null || pending.RegionId != myRegion)
        {
            TempData["Error"] = "That registration is outside your region.";
            return RedirectToAction("PendingParticipants");
        }

        pending.Status = "Rejected";
        await _db.SaveChangesAsync();

        await _email.SendRegistrationRejectedAsync(pending.Email, $"{pending.FirstName} {pending.LastName}", reason);

        TempData["Success"] = $"{pending.FirstName} {pending.LastName} has been rejected.";
        return RedirectToAction("PendingParticipants");
    }

    // ════════════════════════════════════════════════════════════
    //  PARTICIPANTS LIST — region filtered
    // ════════════════════════════════════════════════════════════

    [RequireRole(AppRoles.Raam)]
    public async Task<IActionResult> ParticipantsList()
    {
        SetViewBag();

        var regionId = await GetRaamRegionId();

        var participants = await _db.Users
            .Include(u => u.Region)
            .Include(u => u.Office)
            .Include(u => u.Division)
            .Include(u => u.UserRoles)
            .Where(u => u.RegionId == regionId
                     && u.UserRoles.Any(r => r.Role == AppRoles.Employee)
                     && u.IsExternal == false)
            .OrderBy(u => u.LastName)
            .ToListAsync();

        ViewBag.RegionId = regionId;

        return View("~/Views/Raam/ParticipantsList.cshtml", participants);
    }

    // ── POST: Toggle participant status ────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole(AppRoles.Raam)]
    public async Task<IActionResult> ToggleParticipantStatus(int userId)
    {

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("ParticipantsList");
        }

        // Only participants in the RAAM's own region, and never admin, course manager or RAAM accounts
        var myRegion = await GetRaamRegionId();
        var isPrivileged = await _db.UserRoles.AnyAsync(r => r.UserId == userId
            && (r.Role == AppRoles.Admin || r.Role == AppRoles.CourseManager || r.Role == AppRoles.Raam));
        if (myRegion == null || user.RegionId != myRegion || isPrivileged)
        {
            TempData["Error"] = "You can only change participants in your own region.";
            return RedirectToAction("ParticipantsList");
        }

        user.Status    = user.Status == RecordStatus.Active ? "Inactive" : "Active";
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{user.FirstName} {user.LastName} is now {user.Status.ToLower()}.";
        return RedirectToAction("ParticipantsList");
    }
    // ── POST: Edit employee ─────────────────────────────────────
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Raam)]
public async Task<IActionResult> EmployeeEdit(User model)
{

    var regionId = await GetRaamRegionId();
    var user = await _db.Users.FindAsync(model.UserId);

    if (user == null)
    {
        TempData["Error"] = "Employee not found.";
        return RedirectToAction("ParticipantsList");
    }

    // Block editing employees outside RAAM's own region
    if (user.RegionId != regionId)
    {
        TempData["Error"] = "You do not have access to this employee.";
        return RedirectToAction("ParticipantsList");
    }

    user.FirstName     = model.FirstName;
    user.MiddleName    = model.MiddleName;
    user.LastName      = model.LastName;
    user.ContactNumber = model.ContactNumber;
    user.Position      = model.Position;
    user.Designation   = model.Designation;
    user.SalaryGrade   = model.SalaryGrade;
    user.UpdatedAt     = DateTime.UtcNow;

    await _db.SaveChangesAsync();
    TempData["Success"] = $"{user.FirstName} {user.LastName} updated successfully!";
    return RedirectToAction("ParticipantsList");
}

// ── POST: Assign role ────────────────────────────────────────
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Raam)]
public async Task<IActionResult> AssignRole(int userId, string role)
{

    var regionId = await GetRaamRegionId();
    var user = await _db.Users.FindAsync(userId);

    if (user == null || user.RegionId != regionId)
    {
        TempData["Error"] = "You do not have access to this employee.";
        return RedirectToAction("ParticipantsList");
    }

    var alreadyHasRole = await _db.UserRoles
        .AnyAsync(r => r.UserId == userId && r.Role == role);

    if (!alreadyHasRole && !string.IsNullOrEmpty(role))
    {
        _db.UserRoles.Add(new UserRole
        {
            UserId     = userId,
            Role       = role,
            AssignedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Role '{role}' assigned successfully!";
    }
    else
    {
        TempData["Error"] = "Role already assigned or invalid.";
    }

    return RedirectToAction("ParticipantsList");
}

// ── POST: Remove role ────────────────────────────────────────
[HttpPost]
[ValidateAntiForgeryToken]
[RequireRole(AppRoles.Raam)]
public async Task<IActionResult> RemoveRole(int userId, string role)
{

    var regionId = await GetRaamRegionId();
    var user = await _db.Users.FindAsync(userId);

    if (user == null || user.RegionId != regionId)
    {
        TempData["Error"] = "You do not have access to this employee.";
        return RedirectToAction("ParticipantsList");
    }

    var userRole = await _db.UserRoles
        .FirstOrDefaultAsync(r => r.UserId == userId && r.Role == role);

    if (userRole != null)
    {
        _db.UserRoles.Remove(userRole);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Role '{role}' removed successfully!";
    }

    return RedirectToAction("ParticipantsList");
}

    // ════════════════════════════════════════════════════════════
    //  LOCATIONS
    // ════════════════════════════════════════════════════════════

    [RequireRole(AppRoles.Raam)]
    public async Task<IActionResult> Offices()
    {
        SetViewBag();

        var regionId = await GetRaamRegionId();

        var offices = await _db.Offices
            .Include(o => o.Region)
            .Where(o => o.RegionId == regionId)
            .OrderBy(o => o.OfficeName)
            .ToListAsync();

        ViewBag.RegionId = regionId;

        return View("~/Views/Raam/Offices.cshtml", offices);
    }

    [RequireRole(AppRoles.Raam)]
    public async Task<IActionResult> Divisions()
    {
        SetViewBag();

        var regionId = await GetRaamRegionId();

        var divisions = await _db.Divisions
            .Include(d => d.Office)
                .ThenInclude(o => o.Region)
            .Where(d => d.Office.RegionId == regionId)
            .OrderBy(d => d.DivisionName)
            .ToListAsync();

        ViewBag.RegionId = regionId;

        return View("~/Views/Raam/Divisions.cshtml", divisions);
    }

    // ════════════════════════════════════════════════════════════
    //  AUTO PDF / REPORT (placeholder — build later)
    // ════════════════════════════════════════════════════════════

    [RequireRole(AppRoles.Raam)]
    public async Task<IActionResult> TrainingReport()
    {
        SetViewBag();

        var regionId  = await GetRaamRegionId();
        var region    = await _db.Regions.FindAsync(regionId);

        var trainings = await _db.Trainings
            .Include(t => t.TrainingParticipants)
            .Where(t => t.RegionId == regionId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        ViewBag.RegionName = region?.RegionName ?? "Unknown Region";
        ViewBag.RegionId   = regionId;

        return View("~/Views/Raam/TrainingReport.cshtml", trainings);
    }

    [RequireRole(AppRoles.Raam)]
    public IActionResult AutoPdf()
    {
        SetViewBag();
        return View("~/Views/Raam/AutoPdf.cshtml");
    }
}