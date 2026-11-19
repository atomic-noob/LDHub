using LDHub.Data;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Helpers;

public static class TrainingAccess
{
    /// <summary>
    /// Admin and Course Managers may manage any training. A RAAM may only manage trainings
    /// in their own region. Everyone else may not.
    /// </summary>
    public static async Task<bool> CanManageTrainingAsync(this AppDbContext db, ISession session, int trainingId)
    {
        var role = session.GetString("UserRole");
        if (role == AppRoles.Admin || role == AppRoles.CourseManager) return true;
        if (role != AppRoles.Raam) return false;

        var userId = session.GetUserId();
        if (userId == null) return false;

        var regionId = await db.Users.AsNoTracking()
            .Where(u => u.UserId == userId.Value)
            .Select(u => u.RegionId)
            .FirstOrDefaultAsync();

        return regionId != null
               && await db.Trainings.AnyAsync(t => t.TrainingId == trainingId && t.RegionId == regionId);
    }
}
