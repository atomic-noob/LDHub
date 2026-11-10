using LDHub.Data;
using LDHub.Helpers;
using LDHub.Models;
using LDHub.Models.ViewModels;
using LDHub.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Controllers;

/// <summary>
/// Per-training discussion board with staff announcements.
/// Who can see a training's board:
///   Admin / CourseManager  -> all trainings (staff, can announce)
///   Raam                   -> trainings in their region (staff, can announce)
///   ResourcePerson         -> trainings they are assigned to (staff, can announce)
///   Anyone enrolled        -> that training (participant: can post and reply)
/// </summary>
public class DiscussionController : Controller
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notify;
    private readonly IEmailService _email;

    public DiscussionController(AppDbContext db, INotificationService notify, IEmailService email)
    {
        _db = db;
        _notify = notify;
        _email = email;
    }

    private int? CurrentUserId => HttpContext.Session.GetUserId();
    private string CurrentRole => HttpContext.Session.GetString("UserRole") ?? string.Empty;

    private record Access(bool CanView, bool IsStaff, bool CanAnnounce);

    private async Task<Access> GetAccessAsync(int trainingId, int userId)
    {
        var training = await _db.Trainings.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TrainingId == trainingId);
        if (training == null) return new Access(false, false, false);

        var role = CurrentRole;

        if (role == AppRoles.Admin || role == AppRoles.CourseManager)
            return new Access(true, true, true);

        if (role == AppRoles.Raam)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user?.RegionId != null && user.RegionId == training.RegionId)
                return new Access(true, true, true);
        }

        if (role == AppRoles.ResourcePerson &&
            await _db.TrainingInstructors.AnyAsync(ti => ti.TrainingId == trainingId && ti.UserId == userId))
            return new Access(true, true, true);

        if (await _db.TrainingParticipants.AnyAsync(tp => tp.TrainingId == trainingId && tp.UserId == userId))
            return new Access(true, false, false);

        return new Access(false, false, false);
    }

    // GET: /Discussion  (trainings this user can open a board for)
    public async Task<IActionResult> Index()
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Home");
        var uid = CurrentUserId.Value;
        var role = CurrentRole;

        IQueryable<Training> query = _db.Trainings.AsNoTracking();

        if (role == AppRoles.Admin || role == AppRoles.CourseManager)
        {
            // all trainings
        }
        else if (role == AppRoles.Raam)
        {
            var regionId = (await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == uid))?.RegionId;
            query = query.Where(t => t.RegionId == regionId);
        }
        else if (role == AppRoles.ResourcePerson)
        {
            query = query.Where(t => _db.TrainingInstructors
                .Any(ti => ti.TrainingId == t.TrainingId && ti.UserId == uid));
        }
        else
        {
            query = query.Where(t => _db.TrainingParticipants
                .Any(tp => tp.TrainingId == t.TrainingId && tp.UserId == uid));
        }

        var trainings = await query.OrderByDescending(t => t.StartDate).ToListAsync();
        var ids = trainings.Select(t => t.TrainingId).ToList();

        ViewBag.PostCounts = await _db.TrainingPosts
            .Where(p => ids.Contains(p.TrainingId) && p.ParentId == null)
            .GroupBy(p => p.TrainingId)
            .Select(g => new { TrainingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TrainingId, x => x.Count);

        return View(trainings);
    }

    // GET: /Discussion/Board?trainingId=5
    public async Task<IActionResult> Board(int trainingId)
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Home");
        var uid = CurrentUserId.Value;

        var access = await GetAccessAsync(trainingId, uid);
        if (!access.CanView)
        {
            TempData["Error"] = "You do not have access to this discussion board.";
            return RedirectToAction(nameof(Index));
        }

        var training = await _db.Trainings.AsNoTracking()
            .FirstAsync(t => t.TrainingId == trainingId);

        var posts = await _db.TrainingPosts.AsNoTracking()
            .Include(p => p.User)
            .Where(p => p.TrainingId == trainingId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

        var threads = posts
            .Where(p => p.ParentId == null)
            .OrderByDescending(p => p.IsAnnouncement)   // announcements pinned first
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => new DiscussionThread
            {
                Post = p,
                Replies = posts.Where(r => r.ParentId == p.Id).ToList()
            })
            .ToList();

        return View(new DiscussionBoardViewModel
        {
            Training = training,
            CanAnnounce = access.CanAnnounce,
            IsStaff = access.IsStaff,
            CurrentUserId = uid,
            Threads = threads
        });
    }

    // POST: new discussion thread or announcement
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostThread(int trainingId, string? title, string? body, bool isAnnouncement)
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Home");
        var uid = CurrentUserId.Value;

        var access = await GetAccessAsync(trainingId, uid);
        if (!access.CanView) return RedirectToAction(nameof(Index));

        body = (body ?? string.Empty).Trim();
        if (body.Length == 0 || body.Length > 2000)
        {
            TempData["Error"] = "Your message must be between 1 and 2000 characters.";
            return RedirectToAction(nameof(Board), new { trainingId });
        }

        // Only staff can announce; ignore the flag for everyone else
        if (isAnnouncement && !access.CanAnnounce) isAnnouncement = false;

        title = title?.Trim();
        if (title != null && title.Length > 200) title = title[..200];

        if (isAnnouncement && string.IsNullOrEmpty(title))
        {
            TempData["Error"] = "An announcement needs a title.";
            return RedirectToAction(nameof(Board), new { trainingId });
        }

        _db.TrainingPosts.Add(new TrainingPost
        {
            TrainingId = trainingId,
            UserId = uid,
            IsAnnouncement = isAnnouncement,
            Title = string.IsNullOrEmpty(title) ? null : title,
            Body = body,
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();

        if (isAnnouncement)
        {
            // Everyone in the group except the sender: enrolled participants + assigned resource persons
            var recipients = await _db.TrainingParticipants
                .Where(tp => tp.TrainingId == trainingId)
                .Select(tp => tp.UserId)
                .Union(_db.TrainingInstructors
                    .Where(ti => ti.TrainingId == trainingId)
                    .Select(ti => ti.UserId))
                .ToListAsync();
            recipients.Remove(uid);

            var training = await _db.Trainings.AsNoTracking()
                .FirstAsync(t => t.TrainingId == trainingId);

            await _notify.NotifyAsync(recipients,
                "Announcement: " + title,
                training.TrainingTitle,
                Url.Action(nameof(Board), new { trainingId }));

            // Also email the group (skipped quietly when email is not configured)
            var recipientUsers = await _db.Users.AsNoTracking()
                .Where(u => recipients.Contains(u.UserId))
                .ToListAsync();
            await _email.SendAnnouncementAsync(recipientUsers, training, title!, body);
        }

        TempData["Success"] = isAnnouncement ? "Announcement sent to the group." : "Posted.";
        return RedirectToAction(nameof(Board), new { trainingId });
    }

    // POST: reply in a thread
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int parentId, string? body)
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Home");
        var uid = CurrentUserId.Value;

        var parent = await _db.TrainingPosts
            .FirstOrDefaultAsync(p => p.Id == parentId && p.ParentId == null);
        if (parent == null) return NotFound();

        var access = await GetAccessAsync(parent.TrainingId, uid);
        if (!access.CanView) return RedirectToAction(nameof(Index));

        body = (body ?? string.Empty).Trim();
        if (body.Length == 0 || body.Length > 2000)
        {
            TempData["Error"] = "Your reply must be between 1 and 2000 characters.";
            return RedirectToAction(nameof(Board), new { trainingId = parent.TrainingId });
        }

        _db.TrainingPosts.Add(new TrainingPost
        {
            TrainingId = parent.TrainingId,
            UserId = uid,
            ParentId = parent.Id,
            Body = body,
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();

        // Let the thread's author know (not when replying to yourself)
        if (parent.UserId != uid)
        {
            var name = await _db.Users.AsNoTracking()
                .Where(u => u.UserId == uid)
                .Select(u => u.FirstName + " " + u.LastName)
                .FirstOrDefaultAsync() ?? "Someone";

            await _notify.NotifyAsync(new[] { parent.UserId },
                "New reply to your post",
                name + " replied on the discussion board.",
                Url.Action(nameof(Board), new { trainingId = parent.TrainingId }));
        }

        return RedirectToAction(nameof(Board), new { trainingId = parent.TrainingId });
    }

    // POST: delete a post (its author, or staff of that training). Replies go with it.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Home");
        var uid = CurrentUserId.Value;

        var post = await _db.TrainingPosts.FirstOrDefaultAsync(p => p.Id == id);
        if (post == null) return NotFound();

        var access = await GetAccessAsync(post.TrainingId, uid);
        if (!access.CanView) return RedirectToAction(nameof(Index));

        if (post.UserId != uid && !access.IsStaff)
        {
            TempData["Error"] = "You can only delete your own posts.";
            return RedirectToAction(nameof(Board), new { trainingId = post.TrainingId });
        }

        var trainingId = post.TrainingId;
        _db.TrainingPosts.Remove(post);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Deleted.";
        return RedirectToAction(nameof(Board), new { trainingId });
    }
}
