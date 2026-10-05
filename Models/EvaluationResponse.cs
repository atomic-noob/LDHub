// Models/EvaluationResponse.cs
namespace LDHub.Models
{
    public class EvaluationResponse
    {
        public int Id { get; set; }
        public int TrainingId { get; set; }
        public Training? Training { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public List<EvaluationAnswer> Answers { get; set; } = new();
    }
}