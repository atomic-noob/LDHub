using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;

namespace LDHub.Controllers;

public class RaamController : Controller
{
    private readonly AppDbContext _db;

    public RaamController(AppDbContext db)
    {
        _db = db;
    }

    // ── Auth helpers ───────────────────────────────────────────
    private bool IsRaam() =>
        HttpContext.Session.GetString("UserRole") == "Raam";

    private int GetUserId()
    {
        var str = HttpContext.Session.GetString("UserId");
        return int.TryParse(str, out int id) ? id : 0;
    }

    private void SetViewBag()
    {
        ViewBag.ActiveRole = "Raam";
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

    public async Task<IActionResult> Dashboard()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
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
            .Where(p => p.RegionId == regionId && p.Status == "Inactive")
            .CountAsync();

        var completedCount = await _db.TrainingParticipants
            .Where(tp => trainingIds.Contains(tp.TrainingId)
                      && tp.CompletionStatus == "Completed")
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

    public async Task<IActionResult> Training()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
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

    public async Task<IActionResult> PendingParticipants()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var regionId = await GetRaamRegionId();

        var pending = await _db.PendingParticipants
            .Include(p => p.Region)
            .Include(p => p.Office)
            .Include(p => p.Division)
            .Where(p => p.RegionId == regionId && p.Status == "Inactive")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        ViewBag.RegionId = regionId;

        return View("~/Views/Raam/PendingParticipants.cshtml", pending);
    }

    // ── POST: Approve participant ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveParticipant(int pendingId)
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");

        var pending = await _db.PendingParticipants.FindAsync(pendingId);
        if (pending == null)
        {
            TempData["Error"] = "Participant not found.";
            return RedirectToAction("PendingParticipants");
        }

        // Create user account
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
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow
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

        // Mark pending as approved
        pending.Status = "Active";
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{pending.FirstName} {pending.LastName} approved successfully!";
        return RedirectToAction("PendingParticipants");
    }

    // ── POST: Reject participant ───────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectParticipant(int pendingId, string? reason)
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");

        var pending = await _db.PendingParticipants.FindAsync(pendingId);
        if (pending == null)
        {
            TempData["Error"] = "Participant not found.";
            return RedirectToAction("PendingParticipants");
        }

        pending.Status = "Rejected";
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{pending.FirstName} {pending.LastName} has been rejected.";
        return RedirectToAction("PendingParticipants");
    }

    // ════════════════════════════════════════════════════════════
    //  PARTICIPANTS LIST — region filtered
    // ════════════════════════════════════════════════════════════

    public async Task<IActionResult> ParticipantsList()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var regionId = await GetRaamRegionId();

        var participants = await _db.Users
            .Include(u => u.Region)
            .Include(u => u.Office)
            .Include(u => u.Division)
            .Include(u => u.UserRoles)
            .Where(u => u.RegionId == regionId
                     && u.UserRoles.Any(r => r.Role == "Employee")
                     && u.IsExternal == false)
            .OrderBy(u => u.LastName)
            .ToListAsync();

        ViewBag.RegionId = regionId;

        return View("~/Views/Raam/ParticipantsList.cshtml", participants);
    }

    // ── POST: Toggle participant status ────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleParticipantStatus(int userId)
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("ParticipantsList");
        }

        user.Status    = user.Status == "Active" ? "Inactive" : "Active";
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{user.FirstName} {user.LastName} is now {user.Status.ToLower()}.";
        return RedirectToAction("ParticipantsList");
    }

    // ════════════════════════════════════════════════════════════
    //  LOCATIONS
    // ════════════════════════════════════════════════════════════

    public async Task<IActionResult> Offices()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
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

    public async Task<IActionResult> Divisions()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
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

    public async Task<IActionResult> TrainingReport()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
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

    public IActionResult AutoPdf()
    {
        if (!IsRaam()) return RedirectToAction("Login", "Home");
        SetViewBag();
        return View("~/Views/Raam/AutoPdf.cshtml");
    }
}