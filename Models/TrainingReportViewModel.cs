using LDHub.Models.ViewModels;

namespace LDHub.Models.ViewModels
{
    public class TrainingReportViewModel
    {
        // Training info
        public LDHub.Models.Training Training { get; set; } = null!;

        // Summary stats
        public int     TotalParticipants { get; set; }
        public int     PassedCount       { get; set; }
        public int     FailedCount       { get; set; }
        public int     NotTakenCount     { get; set; }
        public decimal PreTestAvg        { get; set; }
        public decimal PostTestAvg       { get; set; }
        public decimal LearningGain      { get; set; }
        public decimal PassingRate       { get; set; }
        public decimal TotalPoints       { get; set; }

        // Per-participant rows
        public List<ReportRow> Rows { get; set; } = new();

        // T-Test results — full object not individual properties
        public TTestResult? TTest { get; set; }
    }

    public class ReportRow
    {
        public int      UserId       { get; set; }
        public string   Name         { get; set; } = string.Empty;
        public string   Position     { get; set; } = string.Empty;
        public string   Office       { get; set; } = string.Empty;
        public decimal? PreScore     { get; set; }
        public decimal? PostScore    { get; set; }
        public bool     IsPassed     { get; set; }
        public int      PostAttempts { get; set; }
        public decimal? Difference   => PreScore.HasValue && PostScore.HasValue
                                        ? PostScore.Value - PreScore.Value
                                        : null;
    }
}