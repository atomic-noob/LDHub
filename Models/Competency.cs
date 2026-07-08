using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models
{
    [Table("competencies")]
    public class Competency
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("competency_code")]
        [MaxLength(50)]
        public string CompetencyCode { get; set; } = string.Empty;

        [Column("competency_name")]
        [MaxLength(255)]
        public string CompetencyName { get; set; } = string.Empty;

        [Column("competency_title")]
        [MaxLength(255)]
        public string CompetencyTitle { get; set; } = string.Empty;

        [Column("page_number")]
        public int? PageNumber { get; set; }
    }
}