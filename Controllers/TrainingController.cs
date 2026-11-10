using LDHub.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;
using LDHub.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace LDHub.Controllers;
using LDHub.Models.ViewModels;

public class TrainingController : Controller
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly INotificationService _notify;
    private readonly ITrainingReportService _reports;

    public TrainingController(AppDbContext db, IEmailService email, INotificationService notify, ITrainingReportService reports)
    {
        _db = db;
        _email = email;
        _notify = notify;
        _reports = reports;
    }

    private bool CanAccessTraining()
    {
        var role = HttpContext.Session.GetString("UserRole");
        return role == AppRoles.Admin || role == AppRoles.CourseManager || role == AppRoles.Raam;
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
        if (GetUserRole() != AppRoles.Raam) return null;
        var user = await _db.Users.FindAsync(GetUserId());
        return user?.RegionId;
    }

    // ── GET: /Training/Index ───────────────────────────────────
    [RequireRole(AppRoles.Admin, AppRoles.CourseManager, AppRoles.Raam)]
    public async Task<IActionResult> Index()
    {
        SetViewBag();

        var role     = GetUserRole();
        var regionId = await GetRaamRegionId();

        IQueryable<Training> query = _db.Trainings
            .Include(t => t.TrainingParticipants);

        // RAAM only sees their region
        if (role == AppRoles.Raam)
            query = query.Where(t => t.RegionId == regionId);

        var trainings = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        ViewBag.IsRaam   = role == AppRoles.Raam;
        ViewBag.RegionId = regionId;

        return View("~/Views/Training/Index.cshtml", trainings);
    }

    // ── GET: /Training/Create ──────────────────────────────────
    [RequireRole(AppRoles.Admin, AppRoles.CourseManager, AppRoles.Raam)]
    public async Task<IActionResult> Create()
    {
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
        ViewBag.IsRaam       = role == AppRoles.Raam;
        ViewBag.RaamRegionId = regionId;

        return View("~/Views/Training/Create.cshtml");
    }

    // ── POST: /Training/Create ─────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole(AppRoles.Admin, AppRoles.CourseManager, AppRoles.Raam)]
    public async Task<IActionResult> Create(Training model)
    {

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
        if (role == AppRoles.Raam)
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
        ViewBag.IsRaam        = role == AppRoles.Raam;
        ViewBag.RaamRegionId  = regionId;

        return View("~/Views/Training/Create.cshtml", model);
    }

    // ── GET: /Training/Edit/{id} ───────────────────────────────
    [RequireRole(AppRoles.Admin, AppRoles.CourseManager, AppRoles.Raam)]
    public async Task<IActionResult> Edit(int id)
    {
        SetViewBag();

        var training = await _db.Trainings.FindAsync(id);
        if (training == null) return NotFound();

        var role     = GetUserRole();
        var regionId = await GetRaamRegionId();

        // RAAM can only edit trainings in their region
        if (role == AppRoles.Raam && training.RegionId != regionId)
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
        ViewBag.IsRaam        = role == AppRoles.Raam;
        ViewBag.RaamRegionId  = regionId;

        return View("~/Views/Training/Edit.cshtml", training);
    }

    // ── POST: /Training/Edit ───────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole(AppRoles.Admin, AppRoles.CourseManager, AppRoles.Raam)]
    public async Task<IActionResult> Edit(int id, Training model)
    {

        var role     = GetUserRole();
        var regionId = await GetRaamRegionId();

        var training = await _db.Trainings.FindAsync(id);
        if (training == null) return NotFound();

        // RAAM can only edit trainings in their region
        if (role == AppRoles.Raam && training.RegionId != regionId)
        {
            TempData["Error"] = "You do not have access to this training.";
            return RedirectToAction("Index", "Training");
        }

        // Force RAAM's region
        if (role == AppRoles.Raam)
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
            training.RegionId          = role == AppRoles.Raam ? regionId : model.RegionId;
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
        ViewBag.IsRaam        = role == AppRoles.Raam;
        ViewBag.RaamRegionId  = regionId;

        return View("~/Views/Training/Edit.cshtml", model);
    }

    // ── GET: /Training/Details/{id} ────────────────────────────
    [RequireRole(AppRoles.Admin, AppRoles.CourseManager, AppRoles.Raam)]
    public async Task<IActionResult> Details(int id)
    {
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
        if (role == AppRoles.Raam && training.RegionId != regionId)
        {
            TempData["Error"] = "You do not have access to this training.";
            return RedirectToAction("Index", "Training");
        }

        return View("~/Views/Training/Details.cshtml", training);
    }

    // ── GET: /Training/AssignParticipant/{id} ──────────────────
    [RequireRole(AppRoles.Admin, AppRoles.CourseManager, AppRoles.Raam)]
    public async Task<IActionResult> AssignParticipant(int id)
    {
        SetViewBag();

        var training = await _db.Trainings
            .Include(t => t.TrainingParticipants)
                .ThenInclude(tp => tp.User)
            .FirstOrDefaultAsync(t => t.TrainingId == id);

        if (training == null) return NotFound();

        var role     = GetUserRole();
        var regionId = await GetRaamRegionId();

        // RAAM can only assign participants to trainings in their region
        if (role == AppRoles.Raam && training.RegionId != regionId)
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
                .Where(u => u.UserRoles.Any(r => r.Role == AppRoles.Employee)
                        && u.IsExternal == false
                        && u.Status == "Active"
                        && u.InTraining == false
                        && !assignedUserIds.Contains(u.UserId));

        if (role == AppRoles.Raam)
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
[RequireRole(AppRoles.Admin, AppRoles.CourseManager, AppRoles.Raam)]
public async Task<IActionResult> AssignParticipant(int trainingId, List<int> userIds)
{

    userIds ??= new List<int>();
    var newlyAssignedIds = new List<int>();

    // One query for who is already enrolled and one for the users being added
    // (instead of two queries for every selected person)
    var alreadyEnrolled = (await _db.TrainingParticipants
        .Where(tp => tp.TrainingId == trainingId && userIds.Contains(tp.UserId))
        .Select(tp => tp.UserId)
        .ToListAsync()).ToHashSet();

    var usersById = await _db.Users
        .Where(u => userIds.Contains(u.UserId))
        .ToDictionaryAsync(u => u.UserId);

    foreach (var userId in userIds.Distinct())
    {
        if (alreadyEnrolled.Contains(userId)) continue;

        _db.TrainingParticipants.Add(new TrainingParticipant
        {
            TrainingId       = trainingId,
            UserId           = userId,
            EnrollmentDate   = DateTime.UtcNow,
            CompletionStatus = "Enrolled"
        });
        newlyAssignedIds.Add(userId);

        // Mark user as in training
        if (usersById.TryGetValue(userId, out var user))
        {
            user.InTraining = true;
            user.UpdatedAt  = DateTime.UtcNow;
        }
    }

    await _db.SaveChangesAsync();

    // Email only the people who were newly enrolled (never blocks or fails the request)
    if (newlyAssignedIds.Any())
    {
        var training = await _db.Trainings.FindAsync(trainingId);
        var enrolled = await _db.Users.Where(u => newlyAssignedIds.Contains(u.UserId)).ToListAsync();
        if (training != null)
        {
            await _email.SendTrainingAssignedAsync(enrolled, training);
            await _notify.NotifyAsync(newlyAssignedIds,
                "Enrolled: " + training.TrainingTitle,
                "You have been enrolled in this training.",
                Url.Action("Dashboard", "Employee"));
        }
    }

    TempData["Success"] = "Participants assigned successfully!";
    return RedirectToAction("Index", "Training");
}

  // ── GET: /Training/AssignResourcePerson/{id} ───────────────
// REPLACE the existing AssignResourcePerson GET action with this:

[RequireRole(AppRoles.Admin, AppRoles.CourseManager, AppRoles.Raam)]
public async Task<IActionResult> AssignResourcePerson(int id)
{
    SetViewBag();

    var training = await _db.Trainings
        .Include(t => t.TrainingInstructors)
            .ThenInclude(ti => ti.User)
        .Include(t => t.TrainingParticipants)
        .FirstOrDefaultAsync(t => t.TrainingId == id);

    if (training == null) return NotFound();

    var role     = GetUserRole();
    var regionId = await GetRaamRegionId();

    if (role == AppRoles.Raam && training.RegionId != regionId)
    {
        TempData["Error"] = "You do not have access to this training.";
        return RedirectToAction("Index", "Training");
    }

    // Get all active resource persons with Region and Office included
    var resourcePersons = await _db.Users
        .Include(u => u.UserRoles)
        .Include(u => u.Region)
        .Include(u => u.Office)
        .Where(u => u.UserRoles.Any(r => r.Role == AppRoles.ResourcePerson)
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
[RequireRole(AppRoles.Admin, AppRoles.CourseManager, AppRoles.Raam)]
public async Task<IActionResult> AssignResourcePerson(int trainingId, List<int> userIds)
{

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

    var previousInstructorIds = existing.Select(ti => ti.UserId).ToList();
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

    // Email only resource persons who were not already assigned before this save
    var newInstructorIds = userIds.Except(previousInstructorIds).ToList();
    if (newInstructorIds.Any())
    {
        var training = await _db.Trainings.FindAsync(trainingId);
        var instructors = await _db.Users.Where(u => newInstructorIds.Contains(u.UserId)).ToListAsync();
        if (training != null)
        {
            await _email.SendInstructorAssignedAsync(instructors, training);
            await _notify.NotifyAsync(newInstructorIds,
                "Assigned as Resource Person: " + training.TrainingTitle,
                "You have been assigned to this training.",
                Url.Action("Dashboard", "ResourcePerson"));
        }
    }

    TempData["Success"] = "Resource Persons assigned successfully!";
    return RedirectToAction("Index", "Training");
}

    // ── POST: /Training/Delete ─────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireRole(AppRoles.Admin, AppRoles.CourseManager, AppRoles.Raam)]
    public async Task<IActionResult> Delete(int id)
    {

        var training = await _db.Trainings.FindAsync(id);
        if (training == null) return NotFound();

        var role     = GetUserRole();
        var regionId = await GetRaamRegionId();

        // RAAM can only delete trainings in their region
        if (role == AppRoles.Raam && training.RegionId != regionId)
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
[RequireRole(AppRoles.Admin, AppRoles.CourseManager, AppRoles.Raam)]
public async Task<IActionResult> TrainingReport(int? trainingId)
{
    SetViewBag();

    var role     = GetUserRole();
    var regionId = await GetRaamRegionId();

    // Load regions for dropdown (kept for compatibility, though the view no longer uses a dropdown)
    ViewBag.Regions = await _db.Regions.AsNoTracking().OrderBy(r => r.RegionName).ToListAsync();

    IQueryable<Training> trainingsQuery = _db.Trainings.AsNoTracking();

    if (role == AppRoles.Raam)
        trainingsQuery = trainingsQuery.Where(t => t.RegionId == regionId);

    ViewBag.Trainings = await trainingsQuery.OrderByDescending(t => t.CreatedAt).ToListAsync();

    if (trainingId == null) return View("~/Views/Training/TrainingReport.cshtml");

    var training = await _db.Trainings
        .AsNoTracking()
        .Include(t => t.TrainingParticipants)
            .ThenInclude(tp => tp.User)
                .ThenInclude(u => u!.Office)
        .FirstOrDefaultAsync(t => t.TrainingId == trainingId);

    if (training == null) return View("~/Views/Training/TrainingReport.cshtml");

    // ── Block RAAM from viewing a report for a training outside their region ──
    if (role == AppRoles.Raam && training.RegionId != regionId)
    {
        TempData["Error"] = "You do not have access to this training's report.";
        return RedirectToAction("TrainingReport");
    }

    var vm = await _reports.BuildAsync(training);

    return View("~/Views/Training/TrainingReport.cshtml", vm);
}
}