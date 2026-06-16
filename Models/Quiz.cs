using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models;

[Table("quiz")]
public class Quiz
{
    [Key]
    [Column("quiz_id")]
    public int QuizId { get; set; }

    [Column("module_id")]
    public int ModuleId { get; set; }

    [Required]
    [Column("quiz_title")]
    [MaxLength(255)]
    public string QuizTitle { get; set; } = string.Empty;

    [Column("passing_score")]
    public int PassingScore { get; set; } = 70;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("ModuleId")]
    public virtual TrainingModule? Module { get; set; }

    public virtual ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
}

[Table("quiz_questions")]
public class QuizQuestion
{
    [Key]
    [Column("question_id")]
    public int QuestionId { get; set; }

    [Column("quiz_id")]
    public int QuizId { get; set; }

    [Required]
    [Column("question_text")]
    public string QuestionText { get; set; } = string.Empty;

    [ForeignKey("QuizId")]
    public virtual Quiz? Quiz { get; set; }

    public virtual ICollection<QuizChoice> Choices { get; set; } = new List<QuizChoice>();
}

[Table("quiz_choices")]
public class QuizChoice
{
    [Key]
    [Column("choice_id")]
    public int ChoiceId { get; set; }

    [Column("question_id")]
    public int QuestionId { get; set; }

    [Required]
    [Column("choice_text")]
    public string ChoiceText { get; set; } = string.Empty;

    [Column("is_correct")]
    public bool IsCorrect { get; set; } = false;

    [ForeignKey("QuestionId")]
    public virtual QuizQuestion? Question { get; set; }
}