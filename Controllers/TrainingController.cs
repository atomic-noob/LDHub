using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace LDHub.Controllers;
using LDHub.Models.ViewModels;

public class TrainingController : Controller
{
    private readonly AppDbContext _db;

    public TrainingController(AppDbContext db)
    {
        _db = db;
    }

    private bool CanAccessTraining()
    {
        var role = HttpContext.Session.GetString("UserRole");
        return role == "Admin" || role == "CourseManager" || role == "Raam";
    }

    private int GetUserId()
    {
        var str = HttpContext.Session.GetString("UserId");
        return int.TryParse(str, out int id) ? id : 0;
    }

    private string GetUserRole() =>
        HttpContext.Session.GetString("UserRole") ?? "";

    private void SetViewBag()
    {
        ViewBag.ActiveRole = HttpContext.Session.GetString("UserRole");
        ViewBag.UserName   = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto  = HttpContext.Session.GetString("UserPhoto");
    }

    // ── Get RAAM's region ID ───────────────────────────────────
    private async Task<int?> GetRaamRegionId()
    {
        if (GetUserRole() != "Raam") return null;
        var user = await _db.Users.FindAsync(GetUserId());
        return user?.RegionId;
    }

    // ── GET: /Training/Index ───────────────────────────────────
    public async Task<IActionResult> Index()
    {
        if (!CanAccessTraining()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var role     = GetUserRole();
        var regionId = await GetRaamRegionId();

        IQueryable<Training> query = _db.Trainings
            .Include(t => t.TrainingParticipants);

        // RAAM only sees their region
        if (role == "Raam")
            query = query.Where(t => t.RegionId == regionId);

        var trainings = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        ViewBag.IsRaam   = role == "Raam";
        ViewBag.RegionId = regionId;

        return View("~/Views/Training/Index.cshtml", trainings);
    }

    // ── GET: /Training/Create ──────────────────────────────────
    public async Task<IActionResult> Create()
    {
        if (!CanAccessTraining()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var role     = GetUserRole();
        var regionId = await GetRaamRegionId();

        ViewBag.Regions      = await _db.Regions.OrderBy(r => r.RegionName).ToListAsync();
         ViewBag.Proficiencies = new SelectList(
        await _db.ProficiencyLevels.ToListAsync(),
        "ProficiencyId",
        "ProficiencyName"
    );


        ViewBag.Competencies   = await _db.Competencies.OrderBy(c => c.CompetencyCode).ToListAsync();
        ViewBag.IsRaam       = role == "Raam";
        ViewBag.RaamRegionId = regionId;

        return View("~/Views/Training/Create.cshtml");
    }

    // ── POST: /Training/Create ─────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Training model)
    {
        if (!CanAccessTraining()) return RedirectToAction("Login", "Home");

        var role     = GetUserRole();
        var regionId = await GetRaamRegionId();
        
            // Auto-generate training code if empty
            if (string.IsNullOrWhiteSpace(model.TrainingCode))
            {
                var year  = DateTime.Now.Year;
                var count = await _db.Trainings.CountAsync() + 1;
                model.TrainingCode = $"TRN-{year}-{count:D3}";
            }

        // Force RAAM's region
        if (role == "Raam")
            model.RegionId = regionId;

        model.CreatedBy = GetUserId();
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;

        if (ModelState.IsValid)
        {
            _db.Trainings.Add(model);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Training created successfully!";
            return RedirectToAction("Index", "Training");
        }

        ViewBag.Regions       = await _db.Regions.OrderBy(r => r.RegionName).ToListAsync();
        ViewBag.Proficiencies = new SelectList(
                await _db.ProficiencyLevels.ToListAsync(),
                "ProficiencyId",
                "ProficiencyName"
            );
        ViewBag.Competencies   = await _db.Competencies.OrderBy(c => c.CompetencyCode).ToListAsync();
        ViewBag.IsRaam        = role == "Raam";
        ViewBag.RaamRegionId  = regionId;

        return View("~/Views/Training/Create.cshtml", model);
    }

    // ── GET: /Training/Edit/{id} ───────────────────────────────
    public async Task<IActionResult> Edit(int id)
    {
        if (!CanAccessTraining()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var training = await _db.Trainings.FindAsync(id);
        if (training == null) return NotFound();

        var role     = GetUserRole();
        var regionId = await GetRaamRegionId();

        // RAAM can only edit trainings in their region
        if (role == "Raam" && training.RegionId != regionId)
        {
            TempData["Error"] = "You do not have access to this training.";
            return RedirectToAction("Index", "Training");
        }

        ViewBag.Regions       = await _db.Regions.OrderBy(r => r.RegionName).ToListAsync();
      ViewBag.Proficiencies = new SelectList(
    await _db.ProficiencyLevels.ToListAsync(),
    "ProficiencyId",
    "ProficiencyName"
);

        ViewBag.Competencies   = await _db.Competencies.OrderBy(c => c.CompetencyCode).ToListAsync();
        ViewBag.IsRaam        = role == "Raam";
        ViewBag.RaamRegionId  = regionId;

        return View("~/Views/Training/Edit.cshtml", training);
    }

    // ── POST: /Training/Edit ───────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Training model)
    {
        if (!CanAccessTraining()) return RedirectToAction("Login", "Home");

        var role     = GetUserRole();
        var regionId = await GetRaamRegionId();

        var training = await _db.Trainings.FindAsync(id);
        if (training == null) return NotFound();

        // RAAM can only edit trainings in their region
        if (role == "Raam" && training.RegionId != regionId)
        {
            TempData["Error"] = "You do not have access to this training.";
            return RedirectToAction("Index", "Training");
        }

        // Force RAAM's region
        if (role == "Raam")
            model.RegionId = regionId;

        if (ModelState.IsValid)
        {
            training.TrainingCode      = model.TrainingCode;
            training.TrainingTitle     = model.TrainingTitle;
            training.Description       = model.Description;
            training.Location          = model.Location;
            training.StartDate         = model.StartDate;
            training.EndDate           = model.EndDate;
            training.TrainingHrs       = model.TrainingHrs;
            training.DevelopmentAct    = model.DevelopmentAct;
            training.MOVs              = model.MOVs;
            training.SuccessIndicator  = model.SuccessIndicator;
            training.ProficiencyId     = model.ProficiencyId;
            training.RegionId          = role == "Raam" ? regionId : model.RegionId;
            training.UpdatedAt         = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Training updated successfully!";
            return RedirectToAction("Index", "Training");
        }

        ViewBag.Regions       = await _db.Regions.OrderBy(r => r.RegionName).ToListAsync();
       ViewBag.Proficiencies = new SelectList(
    await _db.ProficiencyLevels.ToListAsync(),
    "ProficiencyId",
    "ProficiencyName",
    training.ProficiencyId  // ← this pre-selects the current value
);
ViewBag.Competencies   = await _db.Competencies.OrderBy(c => c.CompetencyCode).ToListAsync();
        ViewBag.IsRaam        = role == "Raam";
        ViewBag.RaamRegionId  = regionId;

        return View("~/Views/Training/Edit.cshtml", model);
    }

    // ── GET: /Training/Details/{id} ────────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        if (!CanAccessTraining()) return RedirectToAction("Login", "Home");
        SetViewBag();

      var training = await _db.Trainings
    .Include(t => t.TrainingParticipants)
        .ThenInclude(tp => tp.User)
    .Include(t => t.TrainingInstructors)
        .ThenInclude(ti => ti.User)
            .ThenInclude(u => u.Office)  // ← add this
    .FirstOrDefaultAsync(t => t.TrainingId == id);

        if (training == null) return NotFound();

        var role     = GetUserRole();
        var regionId = await GetRaamRegionId();

        // RAAM can only view trainings in their region
        if (role == "Raam" && training.RegionId != regionId)
        {
            TempData["Error"] = "You do not have access to this training.";
            return RedirectToAction("Index", "Training");
        }

        return View("~/Views/Training/Details.cshtml", training);
    }

    // ── GET: /Training/AssignParticipant/{id} ──────────────────
    public async Task<IActionResult> AssignParticipant(int id)
    {
        if (!CanAccessTraining()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var training = await _db.Trainings
            .Include(t => t.TrainingParticipants)
                .ThenInclude(tp => tp.User)
            .FirstOrDefaultAsync(t => t.TrainingId == id);

        if (training == null) return NotFound();

        var role     = GetUserRole();
        var regionId = await GetRaamRegionId();

        // RAAM can only assign participants to trainings in their region
        if (role == "Raam" && training.RegionId != regionId)
        {
            TempData["Error"] = "You do not have access to this training.";
            return RedirectToAction("Index", "Training");
        }

       // Get IDs already assigned to this training
            var assignedUserIds = training.TrainingParticipants
                .Select(tp => tp.UserId)
                .ToHashSet();

            IQueryable<User> usersQuery = _db.Users
                .Include(u => u.UserRoles)
                .Include(u => u.Region)
                .Where(u => u.UserRoles.Any(r => r.Role == "Employee")
                        && u.IsExternal == false
                        && u.Status == "Active"
                        && u.InTraining == false
                        && !assignedUserIds.Contains(u.UserId));

        if (role == "Raam")
            usersQuery = usersQuery.Where(u => u.RegionId == regionId);

        var users = await usersQuery
            .OrderBy(u => u.LastName)
            .ToListAsync();

        ViewBag.Training  = training;
       ViewBag.AvailableUsers = users;
       ViewBag.AssignedParticipants = training.TrainingParticipants.ToList();
        return View("~/Views/Training/AssignParticipant.cshtml");
    }

   [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AssignParticipant(int trainingId, List<int> userIds)
{
    if (!CanAccessTraining()) return RedirectToAction("Login", "Home");

    foreach (var userId in userIds)
    {
        var exists = await _db.TrainingParticipants
            .AnyAsync(tp => tp.TrainingId == trainingId && tp.UserId == userId);

        if (!exists)
        {
            _db.TrainingParticipants.Add(new TrainingParticipant
            {
                TrainingId       = trainingId,
                UserId           = userId,
                EnrollmentDate   = DateTime.UtcNow,
                CompletionStatus = "Enrolled"
            });

            // ── Mark user as in training ──────────────────────
            var user = await _db.Users.FindAsync(userId);
            if (user != null)
            {
                user.InTraining = true;
                user.UpdatedAt  = DateTime.UtcNow;
            }
        }
    }

    await _db.SaveChangesAsync();
    TempData["Success"] = "Participants assigned successfully!";
    return RedirectToAction("Index", "Training");
}

  // ── GET: /Training/AssignResourcePerson/{id} ───────────────
// REPLACE the existing AssignResourcePerson GET action with this:

public async Task<IActionResult> AssignResourcePerson(int id)
{
    if (!CanAccessTraining()) return RedirectToAction("Login", "Home");
    SetViewBag();

    var training = await _db.Trainings
        .Include(t => t.TrainingInstructors)
            .ThenInclude(ti => ti.User)
        .Include(t => t.TrainingParticipants)
        .FirstOrDefaultAsync(t => t.TrainingId == id);

    if (training == null) return NotFound();

    var role     = GetUserRole();
    var regionId = await GetRaamRegionId();

    if (role == "Raam" && training.RegionId != regionId)
    {
        TempData["Error"] = "You do not have access to this training.";
        return RedirectToAction("Index", "Training");
    }

    // Get all active resource persons with Region and Office included
    var resourcePersons = await _db.Users
        .Include(u => u.UserRoles)
        .Include(u => u.Region)
        .Include(u => u.Office)
        .Where(u => u.UserRoles.Any(r => r.Role == "ResourcePerson")
                 && u.Status == "Active")
        .OrderBy(u => u.IsExternal)
        .ThenBy(u => u.LastName)
        .ToListAsync();

    ViewBag.Training        = training;
    ViewBag.ResourcePersons = resourcePersons;

    return View("~/Views/Training/AssignResourcePerson.cshtml");
}

// ── POST: /Training/AssignResourcePerson ───────────────────
// REPLACE the existing AssignResourcePerson POST action with this:

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AssignResourcePerson(int trainingId, List<int> userIds)
{
    if (!CanAccessTraining()) return RedirectToAction("Login", "Home");

    // Security check — prevent assigning someone who is already a participant
    var participantIds = await _db.TrainingParticipants
        .Where(tp => tp.TrainingId == trainingId)
        .Select(tp => tp.UserId)
        .ToListAsync();

    var blocked = userIds.Where(id => participantIds.Contains(id)).ToList();
    if (blocked.Any())
    {
        TempData["Error"] = "One or more selected users are already participants in this training and cannot be assigned as Resource Persons.";
        return RedirectToAction("AssignResourcePerson", new { id = trainingId });
    }

    // Remove existing assignments not in the new list
    var existing = await _db.TrainingInstructors
        .Where(ti => ti.TrainingId == trainingId)
        .ToListAsync();

    _db.TrainingInstructors.RemoveRange(existing);

    // Add new assignments
    foreach (var userId in userIds)
    {
        _db.TrainingInstructors.Add(new TrainingInstructor
        {
            TrainingId   = trainingId,
            UserId       = userId,
            AssignedDate = DateTime.UtcNow
        });
    }

    await _db.SaveChangesAsync();
    TempData["Success"] = "Resource Persons assigned successfully!";
    return RedirectToAction("Index", "Training");
}

    // ── POST: /Training/Delete ─────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!CanAccessTraining()) return RedirectToAction("Login", "Home");

        var training = await _db.Trainings.FindAsync(id);
        if (training == null) return NotFound();

        var role     = GetUserRole();
        var regionId = await GetRaamRegionId();

        // RAAM can only delete trainings in their region
        if (role == "Raam" && training.RegionId != regionId)
        {
            TempData["Error"] = "You do not have access to this training.";
            return RedirectToAction("Index", "Training");
        }

        _db.Trainings.Remove(training);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Training deleted successfully!";
        return RedirectToAction("Index", "Training");
    }
  // ── GET: /Training/TrainingReport ─────────────────────────────
// ── GET: /Training/TrainingReport ─────────────────────────────
public async Task<IActionResult> TrainingReport(int? trainingId)
{
    if (!CanAccessTraining()) return RedirectToAction("Login", "Home");
    SetViewBag();

    var role     = GetUserRole();
    var regionId = await GetRaamRegionId();

    // Load regions for dropdown (kept for compatibility, though the view no longer uses a dropdown)
    ViewBag.Regions = await _db.Regions.OrderBy(r => r.RegionName).ToListAsync();

    IQueryable<Training> trainingsQuery = _db.Trainings;

    if (role == "Raam")
        trainingsQuery = trainingsQuery.Where(t => t.RegionId == regionId);

    ViewBag.Trainings = await trainingsQuery.OrderByDescending(t => t.CreatedAt).ToListAsync();

    if (trainingId == null) return View("~/Views/Training/TrainingReport.cshtml");

    var training = await _db.Trainings
        .Include(t => t.TrainingParticipants)
            .ThenInclude(tp => tp.User)
        .FirstOrDefaultAsync(t => t.TrainingId == trainingId);

    if (training == null) return View("~/Views/Training/TrainingReport.cshtml");

    // ── Block RAAM from viewing a report for a training outside their region ──
    if (role == "Raam" && training.RegionId != regionId)
    {
        TempData["Error"] = "You do not have access to this training's report.";
        return RedirectToAction("TrainingReport");
    }

    var rows = new List<ReportRow>();

    foreach (var tp in training.TrainingParticipants)
    {
        if (tp.User == null) continue;

        var preAttempt = await _db.ParticipantTestAttempts
            .Where(e => e.UserId == tp.UserId 
                     && e.TrainingId == trainingId 
                     && e.TestType == "PreTest")
            .OrderByDescending(e => e.TakenAt)
            .FirstOrDefaultAsync();

        var postAttempt = await _db.ParticipantTestAttempts
            .Where(e => e.UserId == tp.UserId 
                     && e.TrainingId == trainingId 
                     && e.TestType == "PostTest")
            .OrderByDescending(e => e.TakenAt)
            .FirstOrDefaultAsync();

        rows.Add(new ReportRow
        {
            UserId       = tp.UserId,
            Name         = $"{tp.User.FirstName} {tp.User.LastName}",
            Position     = tp.User.Position ?? "—",
            Office       = tp.User.Office?.OfficeName ?? "—",
            PreScore     = preAttempt  != null ? (decimal?)preAttempt.Score : null,
            PostScore    = postAttempt != null ? (decimal?)postAttempt.Score: null,
            IsPassed     = postAttempt?.IsPassed ?? false,
            PostAttempts = await _db.ParticipantTestAttempts
                .CountAsync(e => e.UserId == tp.UserId 
                              && e.TrainingId == trainingId 
                              && e.TestType == "PostTest")
        });
    }

    var vm = new TrainingReportViewModel
    {
        Training          = training,
        TotalParticipants = rows.Count,
        PassedCount       = rows.Count(r => r.IsPassed),
        FailedCount       = rows.Count(r => !r.IsPassed && r.PostScore.HasValue),
        NotTakenCount     = rows.Count(r => !r.PostScore.HasValue),
        PreTestAvg        = rows.Any(r => r.PreScore.HasValue)
                            ? Math.Round(rows.Where(r => r.PreScore.HasValue)
                                .Average(r => r.PreScore!.Value), 2)
                            : 0,
        PostTestAvg       = rows.Any(r => r.PostScore.HasValue)
                            ? Math.Round(rows.Where(r => r.PostScore.HasValue)
                                .Average(r => r.PostScore!.Value), 2)
                            : 0,
        PassingRate       = rows.Count > 0
                            ? Math.Round((decimal)rows.Count(r => r.IsPassed) / rows.Count * 100, 2)
                            : 0,
        Rows              = rows
    };

    vm.LearningGain = vm.PostTestAvg - vm.PreTestAvg;
    vm.TotalPoints  = rows.Sum(r => r.PostScore ?? 0);

    var pairs = rows
        .Where(r => r.PreScore.HasValue && r.PostScore.HasValue)
        .Select(r => (Pre: (double)r.PreScore!.Value, Post: (double)r.PostScore!.Value))
        .ToList();

    if (pairs.Count >= 2)
    {
        var pairedPre  = new List<double>();
        var pairedPost = new List<double>();

        foreach (var r in rows)
        {
            if (r.PreScore.HasValue && r.PostScore.HasValue)
            {
                pairedPre.Add((double)r.PreScore.Value);
                pairedPost.Add((double)r.PostScore.Value);
            }
        }

        vm.TTest = LDHub.Services.TTestService.ComputePairedTTest(
            pairedPre, pairedPost, training.TrainingTitle);
    }

    return View("~/Views/Training/TrainingReport.cshtml", vm);
}
}