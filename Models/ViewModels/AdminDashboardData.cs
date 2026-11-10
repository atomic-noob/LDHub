namespace LDHub.Models.ViewModels;

/// <summary>Everything the admin dashboard shows, already counted by the database.</summary>
public class AdminDashboardData
{
    public int TotalEmployees { get; set; }
    public int TotalTrainings { get; set; }
    public int ActiveTrainings { get; set; }
    public int CompletedTrainings { get; set; }
    public int PendingCount { get; set; }
    public int TotalSME { get; set; }
    public int TotalUsers { get; set; }

    public double PassingRate { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalAttempts { get; set; }

    public List<int> MonthlyTrainings { get; set; } = new();
    public List<int> MonthlyRegistrations { get; set; } = new();
    public List<string> RegionLabels { get; set; } = new();
    public List<int> RegionCounts { get; set; } = new();

    public int ScheduledCount { get; set; }
    public int OngoingCount { get; set; }
    public int EndedCount { get; set; }

    public List<Training> RecentTrainings { get; set; } = new();
    public List<PendingParticipant> RecentPending { get; set; } = new();
}
