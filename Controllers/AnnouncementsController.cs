using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LDHub.Data;
using LDHub.Models;

namespace LDHub.Controllers;

public class AnnouncementsController : Controller
{
    private readonly AppDbContext _db;

    public AnnouncementsController(AppDbContext db)
    {
        _db = db;
    }

    // ============================================================
    // INDEX
    // GET: /Announcements
    // ============================================================

    public async Task<IActionResult> Index()
    {
        var announcements = await _db.Announcements
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return View(announcements);
    }

    // ============================================================
    // DETAILS
    // GET: /Announcements/Details/5
    // ============================================================

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var announcement = await _db.Announcements
            .FirstOrDefaultAsync(a => a.AnnouncementId == id);

        if (announcement == null)
        {
            return NotFound();
        }

        return View(announcement);
    }

    // ============================================================
    // CREATE GET
    // ============================================================

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    // ============================================================
    // CREATE POST
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Announcement announcement)
    {
        if (!ModelState.IsValid)
        {
            return View(announcement);
        }

        announcement.CreatedAt = DateTime.Now;
        announcement.UpdatedAt = null;

        _db.Announcements.Add(announcement);

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Announcement created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // EDIT GET
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var announcement =
            await _db.Announcements.FindAsync(id);

        if (announcement == null)
        {
            return NotFound();
        }

        return View(announcement);
    }

    // ============================================================
    // EDIT POST
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        Announcement announcement)
    {
        if (id != announcement.AnnouncementId)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(announcement);
        }

        var existingAnnouncement =
            await _db.Announcements.FindAsync(id);

        if (existingAnnouncement == null)
        {
            return NotFound();
        }

        existingAnnouncement.Title =
            announcement.Title;

        existingAnnouncement.Content =
            announcement.Content;

        existingAnnouncement.IsActive =
            announcement.IsActive;

        existingAnnouncement.UpdatedAt =
            DateTime.Now;

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            "Announcement updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // DELETE GET
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var announcement =
            await _db.Announcements
                .FirstOrDefaultAsync(
                    a => a.AnnouncementId == id);

        if (announcement == null)
        {
            return NotFound();
        }

        return View(announcement);
    }

    // ============================================================
    // DELETE POST
    // ============================================================

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var announcement =
            await _db.Announcements.FindAsync(id);

        if (announcement != null)
        {
            _db.Announcements.Remove(announcement);

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Announcement deleted successfully.";
        }

        return RedirectToAction(nameof(Index));
    }
}