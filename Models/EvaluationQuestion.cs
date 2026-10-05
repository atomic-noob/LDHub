// Models/EvaluationQuestion.cs
namespace LDHub.Models
{
    public class EvaluationQuestion
    {
        public int Id { get; set; }
        public int TrainingId { get; set; }
        public Training? Training { get; set; }

        public string QuestionText { get; set; } = "";
        public string QuestionType { get; set; } = ""; // "rating", "text", "multiple_choice"
        public string? ChoiceA { get; set; }
        public string? ChoiceB { get; set; }
        public string? ChoiceC { get; set; }
        public string? ChoiceD { get; set; }
        public int SortOrder { get; set; }
        public bool IsRequired { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}