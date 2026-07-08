using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models;

[Table("proficiency_levels")]
public class ProficiencyLevel
{
    [Key]
    [Column("proficiency_id")]
    public int ProficiencyId { get; set; }

    [Column("proficiency_name")]
    [MaxLength(100)]
    public string ProficiencyName { get; set; } = string.Empty;

    [Column("score_min")]
    public int? ScoreMin { get; set; }

    [Column("score_max")]
    public int? ScoreMax { get; set; }
}