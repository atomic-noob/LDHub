// Models/EvaluationAnswer.cs
namespace LDHub.Models
{
    public class EvaluationAnswer
    {
        public int Id { get; set; }
        public int EvaluationResponseId { get; set; }
        public EvaluationResponse? EvaluationResponse { get; set; }

        public int EvaluationQuestionId { get; set; }
        public EvaluationQuestion? EvaluationQuestion { get; set; }

        public string? AnswerText { get; set; }
        public int? RatingValue { get; set; } // 1–5, used only if QuestionType == "rating"
    }
}