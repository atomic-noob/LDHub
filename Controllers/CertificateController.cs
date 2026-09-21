using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;

namespace LDHub.Controllers;

public class CertificateController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CertificateController(AppDbContext db, IWebHostEnvironment env)
    {
        _db  = db;
        _env = env;
    }

    private bool CanAccess()
    {
        var role = HttpContext.Session.GetString("UserRole");
        return role == "Admin" || role == "CourseManager" || role == "Raam";
    }

    private async Task<int?> GetRaamRegionId()
    {
        var str = HttpContext.Session.GetString("UserId");
        if (!int.TryParse(str, out int userId)) return null;
        var user = await _db.Users.FindAsync(userId);
        return user?.RegionId;
    }

    private void SetViewBag()
    {
        ViewBag.ActiveRole = HttpContext.Session.GetString("UserRole");
        ViewBag.UserName   = HttpContext.Session.GetString("UserName");
        ViewBag.UserPhoto  = HttpContext.Session.GetString("UserPhoto");
    }

    // ── GET: /Certificate/Index ────────────────────────────────
    public async Task<IActionResult> Index()
    {
        if (!CanAccess()) return RedirectToAction("Login", "Home");
        SetViewBag();

        var role = HttpContext.Session.GetString("UserRole");

        IQueryable<Training> query = _db.Trainings
            .Include(t => t.Region)
            .Include(t => t.TrainingParticipants)
            .OrderByDescending(t => t.CreatedAt);

        // RAAM only sees their region
        if (role == "Raam")
        {
            var regionId = await GetRaamRegionId();
            query = query.Where(t => t.RegionId == regionId);
        }

        var trainings = await query.ToListAsync();
        return View("~/Views/Certificate/Index.cshtml", trainings);
    }

    // ── POST: /Certificate/Upload ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(int trainingId, IFormFile certificate)
    {
        if (!CanAccess()) return RedirectToAction("Login", "Home");

        var training = await _db.Trainings.FindAsync(trainingId);
        if (training == null) return NotFound();

        if (certificate == null || certificate.Length == 0)
        {
            TempData["Error"] = "Please select a file to upload.";
            return RedirectToAction("Index");
        }

        // Validate file type
        var ext = Path.GetExtension(certificate.FileName).ToLower();
        var allowed = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
        if (!allowed.Contains(ext))
        {
            TempData["Error"] = "Only PDF, JPG, and PNG files are allowed.";
            return RedirectToAction("Index");
        }

        // Validate file size (10MB)
        if (certificate.Length > 10485760)
        {
            TempData["Error"] = "File size must not exceed 10MB.";
            return RedirectToAction("Index");
        }

        // Save file
        var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "certificates");
        Directory.CreateDirectory(uploadDir);

        var fileName = $"cert_{trainingId}_{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await certificate.CopyToAsync(stream);
        }

        // Delete old certificate if exists
        if (!string.IsNullOrEmpty(training.CertificatePath))
        {
            var oldPath = Path.Combine(_env.WebRootPath, training.CertificatePath.TrimStart('/'));
            if (System.IO.File.Exists(oldPath))
                System.IO.File.Delete(oldPath);
        }

        training.CertificatePath = $"/uploads/certificates/{fileName}";
        training.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Certificate uploaded successfully for {training.TrainingTitle}!";
        return RedirectToAction("Index");
    }

    // ── POST: /Certificate/Remove ──────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int trainingId)
    {
        if (!CanAccess()) return RedirectToAction("Login", "Home");

        var training = await _db.Trainings.FindAsync(trainingId);
        if (training == null) return NotFound();

        if (!string.IsNullOrEmpty(training.CertificatePath))
        {
            var oldPath = Path.Combine(_env.WebRootPath, training.CertificatePath.TrimStart('/'));
            if (System.IO.File.Exists(oldPath))
                System.IO.File.Delete(oldPath);

            training.CertificatePath = null;
            training.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "Certificate removed successfully.";
        return RedirectToAction("Index");
    }
}
