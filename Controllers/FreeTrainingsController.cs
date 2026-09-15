using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;

namespace LDHub.Controllers;

public class FreeTrainingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public FreeTrainingsController(AppDbContext db, IWebHostEnvironment env)
    {
        _db  = db;
        _env = env;
    }

    private bool IsAdmin() =>
        HttpContext.Session.GetString("UserRole") == "Admin";

    private int GetUserId()
    {
        var str = HttpContext.Session.GetString("UserId");
        return int.TryParse(str, out int id) ? id : 0;
    }

    private void SetViewBag()
    {
        ViewBag.ActiveRole = "Admin";
        ViewBag.UserName   = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto  = HttpContext.Session.GetString("UserPhoto");
    }

    // ── GET: /FreeTrainings/Index ──────────────────────────────
    public async Task<IActionResult> Index()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Admin");
        SetViewBag();

        var trainings = await _db.FreeTrainings
            .Include(t => t.Region)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return View("~/Views/FreeTrainings/Index.cshtml", trainings);
    }

    // ── GET: /FreeTrainings/Create ─────────────────────────────
    public async Task<IActionResult> Create()
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Admin");
        SetViewBag();

        ViewBag.Regions = await _db.Regions.OrderBy(r => r.RegionName).ToListAsync();
        return View("~/Views/FreeTrainings/Create.cshtml");
    }

    // ── POST: /FreeTrainings/Create ────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FreeTraining model, IFormFile? photo)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Admin");

        // Validate Monday-Friday only
        if (model.StartDate.HasValue && (model.StartDate.Value.DayOfWeek == DayOfWeek.Saturday || model.StartDate.Value.DayOfWeek == DayOfWeek.Sunday))
        {
            ModelState.AddModelError("StartDate", "Start date must be a weekday (Monday to Friday).");
        }
        if (model.EndDate.HasValue && (model.EndDate.Value.DayOfWeek == DayOfWeek.Saturday || model.EndDate.Value.DayOfWeek == DayOfWeek.Sunday))
        {
            ModelState.AddModelError("EndDate", "End date must be a weekday (Monday to Friday).");
        }

        if (ModelState.IsValid)
        {
            // Handle photo upload
            if (photo != null && photo.Length > 0)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "free-trainings");
                Directory.CreateDirectory(uploadDir);
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(photo.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await photo.CopyToAsync(stream);
                model.Photo = $"/uploads/free-trainings/{fileName}";
            }

            model.CreatedBy = GetUserId();
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;
            model.IsActive  = true;

            _db.FreeTrainings.Add(model);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Free training posted successfully!";
            return RedirectToAction("Index");
        }

        ViewBag.Regions = await _db.Regions.OrderBy(r => r.RegionName).ToListAsync();
        return View("~/Views/FreeTrainings/Create.cshtml", model);
    }

    // ── GET: /FreeTrainings/Edit/{id} ──────────────────────────
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Admin");
        SetViewBag();

        var training = await _db.FreeTrainings.FindAsync(id);
        if (training == null) return NotFound();

        ViewBag.Regions = await _db.Regions.OrderBy(r => r.RegionName).ToListAsync();
        return View("~/Views/FreeTrainings/Edit.cshtml", training);
    }

    // ── POST: /FreeTrainings/Edit ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FreeTraining model, IFormFile? photo)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Admin");

        var training = await _db.FreeTrainings.FindAsync(id);
        if (training == null) return NotFound();

        // Validate Monday-Friday only
        if (model.StartDate.HasValue && (model.StartDate.Value.DayOfWeek == DayOfWeek.Saturday || model.StartDate.Value.DayOfWeek == DayOfWeek.Sunday))
        {
            TempData["Error"] = "Start date must be a weekday (Monday to Friday).";
            ViewBag.Regions = await _db.Regions.OrderBy(r => r.RegionName).ToListAsync();
            return View("~/Views/FreeTrainings/Edit.cshtml", model);
        }

        training.Title            = model.Title;
        training.Description      = model.Description;
        training.Location         = model.Location;
        training.RegionId         = model.RegionId;
        training.StartDate        = model.StartDate;
        training.EndDate          = model.EndDate;
        training.RegistrationLink = model.RegistrationLink;
        training.IsActive         = model.IsActive;
        training.UpdatedAt        = DateTime.UtcNow;

        if (photo != null && photo.Length > 0)
        {
            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "free-trainings");
            Directory.CreateDirectory(uploadDir);
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(photo.FileName)}";
            var filePath = Path.Combine(uploadDir, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await photo.CopyToAsync(stream);
            training.Photo = $"/uploads/free-trainings/{fileName}";
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Free training updated successfully!";
        return RedirectToAction("Index");
    }

    // ── POST: /FreeTrainings/Delete ────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Admin");

        var training = await _db.FreeTrainings.FindAsync(id);
        if (training != null)
        {
            _db.FreeTrainings.Remove(training);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Free training deleted.";
        }

        return RedirectToAction("Index");
    }

    // ── POST: /FreeTrainings/ToggleActive ──────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        if (!IsAdmin()) return RedirectToAction("Index", "Admin");

        var training = await _db.FreeTrainings.FindAsync(id);
        if (training != null)
        {
            training.IsActive  = !training.IsActive;
            training.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Training is now {(training.IsActive ? "active" : "inactive")}.";
        }

        return RedirectToAction("Index");
    }
}
