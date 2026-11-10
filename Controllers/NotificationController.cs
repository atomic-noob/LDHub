using LDHub.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Controllers;

public class NotificationController : Controller
{
    private readonly AppDbContext _db;

    public NotificationController(AppDbContext db)
    {
        _db = db;
    }

    private int? CurrentUserId => HttpContext.Session.GetInt32("UserId");

    // GET: /Notification  (inbox, latest 100)
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId;
        if (userId == null) return RedirectToAction("Login", "Home");

        var items = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .ToListAsync();

        return View(items);
    }

    // GET: /Notification/Summary  (JSON for the navbar bell)
    [HttpGet]
    public async Task<IActionResult> Summary()
    {
        var userId = CurrentUserId;
        if (userId == null) return Json(new { unread = 0, items = Array.Empty<object>() });

        var unread = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        var latest = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(6)
            .ToListAsync();

        var items = latest.Select(n => new
        {
            id = n.Id,
            title = n.Title,
            message = n.Message,
            isRead = n.IsRead,
            time = TimeAgo(n.CreatedAt)
        });

        return Json(new { unread, items });
    }

    // GET: /Notification/Open/5  (marks it read, then goes to its link)
    [HttpGet]
    public async Task<IActionResult> Open(int id)
    {
        var userId = CurrentUserId;
        if (userId == null) return RedirectToAction("Login", "Home");

        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n == null) return RedirectToAction(nameof(Index));

        if (!n.IsRead)
        {
            n.IsRead = true;
            await _db.SaveChangesAsync();
        }

        // Only follow links that stay on this site
        if (!string.IsNullOrEmpty(n.Url) && Url.IsLocalUrl(n.Url))
            return LocalRedirect(n.Url);

        return RedirectToAction(nameof(Index));
    }

    // POST: /Notification/MarkAllRead
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = CurrentUserId;
        if (userId == null) return RedirectToAction("Login", "Home");

        var unread = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread) n.IsRead = true;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private static string TimeAgo(DateTime when)
    {
        var span = DateTime.Now - when;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} d ago";
        return when.ToString("MMM d, yyyy");
    }
}
