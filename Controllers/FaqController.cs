using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Models;
using LDHub.Data;

namespace LDHub.Controllers;

public class FaqController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public FaqController(AppDbContext db, IWebHostEnvironment env)
    {
        _db  = db;
        _env = env;
    }

    // ── Auth guard ────────────────────────────────────────────────
    private bool IsAdmin()
        => HttpContext.Session.GetString("UserRole") == "Admin";

    private IActionResult AdminOnly()
        => RedirectToAction("Login", "Home");


    // ══════════════════════════════════════════════════════════════
    // FAQ CRUD
    // ══════════════════════════════════════════════════════════════

    // ── GET: /Faq/Index ───────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        if (!IsAdmin()) return AdminOnly();

        var faqs = await _db.Faqs
            .OrderBy(f => f.DisplayOrder)
            .ThenBy(f => f.CreatedAt)
            .ToListAsync();

        return View(faqs);
    }

    // ── GET: /Faq/Create ──────────────────────────────────────────
    public IActionResult Create()
    {
        if (!IsAdmin()) return AdminOnly();
        return View(new Faq());
    }

    // ── POST: /Faq/Create ─────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Faq model, IFormFile? imageFile)
    {
        if (!IsAdmin()) return AdminOnly();

        // Remove ImagePath from validation — we handle it manually
        ModelState.Remove("ImagePath");

        if (!ModelState.IsValid)
            return View(model);

        // Handle image upload
        if (imageFile != null && imageFile.Length > 0)
        {
            var uploadPath = Path.Combine(
                _env.WebRootPath, "uploads", "faqs");

            Directory.CreateDirectory(uploadPath);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";
            var filePath = Path.Combine(uploadPath, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await imageFile.CopyToAsync(stream);

            model.ImagePath = $"/uploads/faqs/{fileName}";
        }

        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = DateTime.UtcNow;

        _db.Faqs.Add(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = "FAQ created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ── GET: /Faq/Edit/{id} ───────────────────────────────────────
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsAdmin()) return AdminOnly();

        var faq = await _db.Faqs.FindAsync(id);
        if (faq == null) return NotFound();

        return View(faq);
    }

    // ── POST: /Faq/Edit/{id} ──────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Faq model, IFormFile? imageFile)
    {
        if (!IsAdmin()) return AdminOnly();
        if (id != model.Id) return BadRequest();

        ModelState.Remove("ImagePath");

        if (!ModelState.IsValid)
            return View(model);

        var existing = await _db.Faqs.FindAsync(id);
        if (existing == null) return NotFound();

        // Handle image upload
        if (imageFile != null && imageFile.Length > 0)
        {
            // Delete old image if exists
            if (!string.IsNullOrEmpty(existing.ImagePath))
            {
                var oldPath = Path.Combine(
                    _env.WebRootPath,
                    existing.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            var uploadPath = Path.Combine(
                _env.WebRootPath, "uploads", "faqs");

            Directory.CreateDirectory(uploadPath);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imageFile.FileName)}";
            var filePath = Path.Combine(uploadPath, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await imageFile.CopyToAsync(stream);

            existing.ImagePath = $"/uploads/faqs/{fileName}";
        }

        existing.Question     = model.Question;
        existing.Answer       = model.Answer;
        existing.DisplayOrder = model.DisplayOrder;
        existing.IsActive     = model.IsActive;
        existing.UpdatedAt    = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["Success"] = "FAQ updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ── POST: /Faq/Delete/{id} ────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdmin()) return AdminOnly();

        var faq = await _db.Faqs.FindAsync(id);
        if (faq == null) return NotFound();

        // Delete image file if exists
        if (!string.IsNullOrEmpty(faq.ImagePath))
        {
            var imgPath = Path.Combine(
                _env.WebRootPath,
                faq.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (System.IO.File.Exists(imgPath))
                System.IO.File.Delete(imgPath);
        }

        _db.Faqs.Remove(faq);
        await _db.SaveChangesAsync();

        TempData["Success"] = "FAQ deleted.";
        return RedirectToAction(nameof(Index));
    }


    // ══════════════════════════════════════════════════════════════
    // INQUIRIES
    // ══════════════════════════════════════════════════════════════

    // ── GET: /Faq/Inquiries ───────────────────────────────────────
    public async Task<IActionResult> Inquiries()
    {
        if (!IsAdmin()) return AdminOnly();

        var inquiries = await _db.Inquiries
            .OrderByDescending(i => i.SubmittedAt)
            .ToListAsync();

        return View(inquiries);
    }

    // ── POST: /Faq/MarkRead/{id} ──────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        if (!IsAdmin()) return AdminOnly();

        var inquiry = await _db.Inquiries.FindAsync(id);
        if (inquiry == null) return NotFound();

        inquiry.IsRead = true;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Inquiries));
    }

    // ── POST: /Faq/DeleteInquiry/{id} ────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInquiry(int id)
    {
        if (!IsAdmin()) return AdminOnly();

        var inquiry = await _db.Inquiries.FindAsync(id);
        if (inquiry == null) return NotFound();

        _db.Inquiries.Remove(inquiry);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Inquiry deleted.";
        return RedirectToAction(nameof(Inquiries));
    }

    // ── POST: /Faq/SubmitInquiry — public form submit ─────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitInquiry(Inquiry model)
    {
        if (!ModelState.IsValid)
        {
            // Return JSON so the homepage JS can handle it
            return Json(new { success = false, message = "Please fill in all fields correctly." });
        }

        model.SubmittedAt = DateTime.UtcNow;
        model.IsRead      = false;

        _db.Inquiries.Add(model);
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = "Your inquiry has been submitted. We will get back to you soon!" });
    }
}
