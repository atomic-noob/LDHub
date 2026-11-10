using LDHub.Data;
using LDHub.Models.ViewModels;
using LDHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly AppDbContext _db;

    public AdminDashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdminDashboardData> GetAsync(DateTime now)
    {
        // ── KPI Stats (all counted in the database) ───────────────
        var totalEmployees  = await _db.Users
            .Where(u => u.UserRoles.Any(r => r.Role == AppRoles.Employee) && u.IsExternal == false)
            .CountAsync();

        var totalTrainings  = await _db.Trainings.CountAsync();

        var activeTrainings = await _db.Trainings
            .Where(t => t.StartDate <= now && t.EndDate >= now)
            .CountAsync();

        var completedTrainings = await _db.Trainings
            .Where(t => t.EndDate < now)
            .CountAsync();

        var pendingCount = await _db.PendingParticipants
            .Where(p => p.Status == "Inactive")
            .CountAsync();

        var totalSME = await _db.Users
            .Where(u => u.UserRoles.Any(r => r.Role == AppRoles.ResourcePerson) && u.IsExternal == true)
            .CountAsync();

        var totalUsers = await _db.Users.CountAsync();

        // ── Passing Rate ───────────────────────────────────────────
        var totalAttempts = await _db.ParticipantTestAttempts
            .CountAsync(a => a.TestType == TestTypes.PostTest);
        var passedCount = await _db.ParticipantTestAttempts
            .CountAsync(a => a.TestType == TestTypes.PostTest && a.IsPassed);
        var passingRate = totalAttempts > 0
            ? Math.Round((double)passedCount / totalAttempts * 100, 1)
            : 0;

        // ── Monthly Trainings (Bar Chart) ──────────────────────────
        var year = now.Year;
        var trainingsByMonth = await _db.Trainings
            .Where(t => t.CreatedAt.Year == year)
            .GroupBy(t => t.CreatedAt.Month)
            .Select(g => new { Month = g.Key, Count = g.Count() })
            .ToListAsync();
        var monthlyTrainings = Enumerable.Range(1, 12)
            .Select(m => trainingsByMonth.FirstOrDefault(x => x.Month == m)?.Count ?? 0)
            .ToList();

        // ── Training Status (Pie Chart) ────────────────────────────
        var scheduledCount = await _db.Trainings.CountAsync(t => t.StartDate.HasValue && t.StartDate > now);
        var ongoingCount   = await _db.Trainings.CountAsync(t => t.StartDate <= now && t.EndDate >= now);
        var endedCount     = await _db.Trainings.CountAsync(t => t.EndDate.HasValue && t.EndDate < now);

        // ── Participants per Region (Bar Chart) ────────────────────
        var regionCounts = await _db.Users
            .Where(u => u.RegionId != null && u.UserRoles.Any(ur => ur.Role == AppRoles.Employee))
            .GroupBy(u => u.RegionId)
            .Select(g => new { RegionId = g.Key, Count = g.Count() })
            .ToListAsync();
        var regionNames = await _db.Regions
            .AsNoTracking()
            .ToDictionaryAsync(r => r.RegionId, r => r.RegionName);
        var participantsByRegion = regionNames.Select(r => new
        {
            RegionName = r.Value,
            Count      = regionCounts.FirstOrDefault(c => c.RegionId == r.Key)?.Count ?? 0
        }).OrderByDescending(r => r.Count).Take(10).ToList();

        // ── Employee Registrations per Month (Line Chart) ──────────
        var registrationsByMonth = await _db.Users
            .Where(u => u.CreatedAt.Year == year)
            .GroupBy(u => u.CreatedAt.Month)
            .Select(g => new { Month = g.Key, Count = g.Count() })
            .ToListAsync();
        var monthlyRegistrations = Enumerable.Range(1, 12)
            .Select(m => registrationsByMonth.FirstOrDefault(x => x.Month == m)?.Count ?? 0)
            .ToList();

        // ── Pass vs Fail (Pie Chart) ───────────────────────────────
        var failedCount2 = totalAttempts - passedCount;

        // ── Recent Trainings (only the 5 shown, not every training) ─
        var recentTrainings = await _db.Trainings
            .AsNoTracking()
            .Include(t => t.TrainingParticipants)
            .Include(t => t.Region)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .ToListAsync();

        // ── Recent Pending ─────────────────────────────────────────
        var recentPending = await _db.PendingParticipants
            .AsNoTracking()
            .Where(p => p.Status == "Inactive")
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .ToListAsync();

        return new AdminDashboardData
        {
            TotalEmployees       = totalEmployees,
            TotalTrainings       = totalTrainings,
            ActiveTrainings      = activeTrainings,
            CompletedTrainings   = completedTrainings,
            PendingCount         = pendingCount,
            TotalSME             = totalSME,
            TotalUsers           = totalUsers,
            PassingRate          = passingRate,
            PassedCount          = passedCount,
            FailedCount          = failedCount2,
            TotalAttempts        = totalAttempts,
            MonthlyTrainings     = monthlyTrainings,
            MonthlyRegistrations = monthlyRegistrations,
            RegionLabels         = participantsByRegion.Select(r => r.RegionName).ToList(),
            RegionCounts         = participantsByRegion.Select(r => r.Count).ToList(),
            ScheduledCount       = scheduledCount,
            OngoingCount         = ongoingCount,
            EndedCount           = endedCount,
            RecentTrainings      = recentTrainings,
            RecentPending        = recentPending
        };
    }
}
