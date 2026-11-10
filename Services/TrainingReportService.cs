using LDHub.Data;
using LDHub.Models;
using LDHub.Models.ViewModels;
using LDHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Services;

public class TrainingReportService : ITrainingReportService
{
    private readonly AppDbContext _db;

    public TrainingReportService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TrainingReportViewModel> BuildAsync(Training training)
    {
        var trainingId = training.TrainingId;

        // One query for every participant's tests (instead of three queries per participant)
        var attempts = await _db.ParticipantTestAttempts
            .AsNoTracking()
            .Where(e => e.TrainingId == trainingId
                     && (e.TestType == TestTypes.PreTest || e.TestType == TestTypes.PostTest))
            .ToListAsync();
        var attemptsByUser = attempts.ToLookup(e => e.UserId);

        var rows = new List<ReportRow>();

        foreach (var tp in training.TrainingParticipants)
        {
            if (tp.User == null) continue;

            var mine         = attemptsByUser[tp.UserId].ToList();
            var preAttempt   = mine.Where(e => e.TestType == TestTypes.PreTest)
                                   .OrderByDescending(e => e.TakenAt).FirstOrDefault();
            var postAttempts = mine.Where(e => e.TestType == TestTypes.PostTest).ToList();
            var postAttempt  = postAttempts.OrderByDescending(e => e.TakenAt).FirstOrDefault();

            rows.Add(new ReportRow
            {
                UserId       = tp.UserId,
                Name         = $"{tp.User.FirstName} {tp.User.LastName}",
                Position     = tp.User.Position ?? "—",
                Office       = tp.User.Office?.OfficeName ?? "—",
                PreScore     = preAttempt  != null ? (decimal?)preAttempt.Score : null,
                PostScore    = postAttempt != null ? (decimal?)postAttempt.Score : null,
                IsPassed     = postAttempt?.IsPassed ?? false,
                PostAttempts = postAttempts.Count
            });
        }

        var vm = new TrainingReportViewModel
        {
            Training          = training,
            TotalParticipants = rows.Count,
            PassedCount       = rows.Count(r => r.IsPassed),
            FailedCount       = rows.Count(r => !r.IsPassed && r.PostScore.HasValue),
            NotTakenCount     = rows.Count(r => !r.PostScore.HasValue),
            PreTestAvg        = rows.Any(r => r.PreScore.HasValue)
                                ? Math.Round(rows.Where(r => r.PreScore.HasValue)
                                    .Average(r => r.PreScore!.Value), 2)
                                : 0,
            PostTestAvg       = rows.Any(r => r.PostScore.HasValue)
                                ? Math.Round(rows.Where(r => r.PostScore.HasValue)
                                    .Average(r => r.PostScore!.Value), 2)
                                : 0,
            PassingRate       = rows.Count > 0
                                ? Math.Round((decimal)rows.Count(r => r.IsPassed) / rows.Count * 100, 2)
                                : 0,
            Rows              = rows
        };

        vm.LearningGain = vm.PostTestAvg - vm.PreTestAvg;
        vm.TotalPoints  = rows.Sum(r => r.PostScore ?? 0);

        var pairs = rows
            .Where(r => r.PreScore.HasValue && r.PostScore.HasValue)
            .Select(r => (Pre: (double)r.PreScore!.Value, Post: (double)r.PostScore!.Value))
            .ToList();

        if (pairs.Count >= 2)
        {
            var pairedPre  = new List<double>();
            var pairedPost = new List<double>();

            foreach (var r in rows)
            {
                if (r.PreScore.HasValue && r.PostScore.HasValue)
                {
                    pairedPre.Add((double)r.PreScore.Value);
                    pairedPost.Add((double)r.PostScore.Value);
                }
            }

            vm.TTest = LDHub.Services.TTestService.ComputePairedTTest(
                pairedPre, pairedPost, training.TrainingTitle);
        }

        return vm;
    }
}
