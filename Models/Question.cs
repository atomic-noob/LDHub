using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models;

[Table("questions")]
public class Question
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("training_id")]
    public int TrainingId { get; set; }

    [Required]
    [Column("question_type")]
    [MaxLength(50)]
    public string QuestionType { get; set; } = string.Empty;

    [Required]
    [Column("question_text")]
    public string QuestionText { get; set; } = string.Empty;

    [Column("choice_a")]
    public string? ChoiceA { get; set; }

    [Column("choice_b")]
    public string? ChoiceB { get; set; }

    [Column("choice_c")]
    public string? ChoiceC { get; set; }

    [Column("choice_d")]
    public string? ChoiceD { get; set; }

    [Column("correct_answer")]
    public string? CorrectAnswer { get; set; }

    [Column("points")]
    public decimal Points { get; set; } = 1;

    [ForeignKey("TrainingId")]
    public virtual Training? Training { get; set; }
}