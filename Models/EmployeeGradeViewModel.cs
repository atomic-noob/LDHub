using LDHub.Models;

namespace LDHub.Models.ViewModels
{
    public class EmployeeGradeViewModel
    {
        public Training? Training       { get; set; }
        public int       TrainingId     { get; set; }
        public decimal?  PreTestScore   { get; set; }
        public decimal?  PostTestScore  { get; set; }
        public bool      PostTestPassed { get; set; }
        public int       PostAttempts   { get; set; }
        public decimal   TotalPoints    { get; set; }
        public string    CompletionStatus { get; set; } = string.Empty;
        public DateTime  EnrollmentDate { get; set; }
        public List<QuizAttempt> QuizAttempts { get; set; } = new();

        public decimal? Difference => PreTestScore.HasValue && PostTestScore.HasValue
            ? PostTestScore.Value - PreTestScore.Value : null;
    }
}
