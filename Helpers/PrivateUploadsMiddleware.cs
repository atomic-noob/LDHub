using LDHub.Data;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Helpers;

/// <summary>
/// Puts a login check in front of private uploaded files that live under wwwroot/uploads:
///   /uploads/modules/*        training material: staff, or people tied to that training
///   /uploads/certificates/*   certificate templates: staff, or people tied to that training
///   /uploads/profile-photos/* any logged-in user
/// Public images (faqs, free-trainings) are not touched. Must run after UseSession and before UseStaticFiles.
/// </summary>
public class PrivateUploadsMiddleware
{
    private const string Modules       = "/uploads/modules/";
    private const string Certificates  = "/uploads/certificates/";
    private const string ProfilePhotos = "/uploads/profile-photos/";

    private readonly RequestDelegate _next;

    public PrivateUploadsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, AppDbContext db)
    {
        var path = ctx.Request.Path.Value ?? string.Empty;
        var isModule = path.StartsWith(Modules, StringComparison.OrdinalIgnoreCase);
        var isCert   = path.StartsWith(Certificates, StringComparison.OrdinalIgnoreCase);
        var isPhoto  = path.StartsWith(ProfilePhotos, StringComparison.OrdinalIgnoreCase);

        if (!isModule && !isCert && !isPhoto)
        {
            await _next(ctx);
            return;
        }

        var userId = ctx.Session.GetUserId();
        if (userId == null)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        ctx.Response.Headers["Cache-Control"] = "private, no-store";

        // Profile photos: any logged-in user
        if (isPhoto)
        {
            await _next(ctx);
            return;
        }

        var role = ctx.Session.GetString("UserRole") ?? string.Empty;
        if (role is AppRoles.Admin or AppRoles.CourseManager)
        {
            await _next(ctx);
            return;
        }

        // Which training does this file belong to?
        int? trainingId = isModule
            ? await db.TrainingModules.AsNoTracking()
                .Where(m => m.FilePath == path)
                .Select(m => (int?)m.TrainingId)
                .FirstOrDefaultAsync()
            : await db.Trainings.AsNoTracking()
                .Where(t => t.CertificatePath == path)
                .Select(t => (int?)t.TrainingId)
                .FirstOrDefaultAsync();

        if (trainingId == null)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var uid = userId.Value;
        var tid = trainingId.Value;

        var allowed = await db.TrainingParticipants.AnyAsync(tp => tp.TrainingId == tid && tp.UserId == uid);

        if (!allowed && role == AppRoles.ResourcePerson)
            allowed = await db.TrainingInstructors.AnyAsync(ti => ti.TrainingId == tid && ti.UserId == uid);

        if (!allowed && role == AppRoles.Raam)
        {
            var regionId = await db.Users.AsNoTracking()
                .Where(u => u.UserId == uid)
                .Select(u => u.RegionId)
                .FirstOrDefaultAsync();
            allowed = regionId != null
                      && await db.Trainings.AnyAsync(t => t.TrainingId == tid && t.RegionId == regionId);
        }

        if (!allowed)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await _next(ctx);
    }
}
