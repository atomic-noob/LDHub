using LDHub.Data;
using LDHub.Models;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Helpers;

public static class NotificationAccess
{
    /// <summary>
    /// The user's notifications that fit the role they are acting as right now.
    /// A notification tied to a training is shown only when that role can reach the training:
    ///   Admin, Course Manager  -> all trainings
    ///   RAAM                   -> trainings in their own region
    ///   Resource Person        -> trainings they are assigned to
    ///   Participant            -> trainings they are enrolled in
    /// Notifications that are not tied to a training (for example a new registration) always show.
    /// </summary>
    public static async Task<IQueryable<Notification>> VisibleNotificationsAsync(
        this AppDbContext db, int userId, string? role)
    {
        var mine = db.Notifications.Where(n => n.UserId == userId);

        if (role == AppRoles.Admin || role == AppRoles.CourseManager)
            return mine;

        if (role == AppRoles.Raam)
        {
            var regionId = await db.Users.AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => u.RegionId)
                .FirstOrDefaultAsync();

            if (regionId == null)
                return mine.Where(n => n.TrainingId == null);

            return mine.Where(n => n.TrainingId == null
                || db.Trainings.Any(t => t.TrainingId == n.TrainingId && t.RegionId == regionId));
        }

        if (role == AppRoles.ResourcePerson)
            return mine.Where(n => n.TrainingId == null
                || db.TrainingInstructors.Any(ti => ti.TrainingId == n.TrainingId && ti.UserId == userId));

        // Participants (and anyone else): only trainings they are enrolled in
        return mine.Where(n => n.TrainingId == null
            || db.TrainingParticipants.Any(tp => tp.TrainingId == n.TrainingId && tp.UserId == userId));
    }
}
