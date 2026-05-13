using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LDHub.Models
{
    // ── REGION ────────────────────────────────────────────────────
    // Maps to the 'region' table
    // Top level of location hierarchy
    // e.g. "MIMAROPA Region - REGION IV-B"
    [Table("region")]
    public class Region
    {
        [Key]
        [Column("region_id")]
        public int RegionId { get; set; }

        [Required]
        [Column("region_name")]
        [MaxLength(255)]
        public string RegionName { get; set; } = string.Empty;

        [Column("region_code")]
        [MaxLength(50)]
        public string? RegionCode { get; set; }

        // One region has MANY offices
        public virtual ICollection<Office> Offices { get; set; } = new List<Office>();

        // One region has MANY users assigned to it
        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }

    // ── OFFICE ────────────────────────────────────────────────────
    // Maps to the 'office' table
    // Second level — belongs to a Region
    // e.g. "Finance and Management Service (FMS)"
    [Table("office")]
    public class Office
    {
        [Key]
        [Column("office_id")]
        public int OfficeId { get; set; }

        [Required]
        [Column("office_name")]
        [MaxLength(255)]
        public string OfficeName { get; set; } = string.Empty;

        [Column("office_code")]
        [MaxLength(50)]
        public string? OfficeCode { get; set; }

        // Foreign key — which region this office belongs to
        [Column("region_id")]
        public int RegionId { get; set; }

        [ForeignKey("RegionId")]
        public virtual Region? Region { get; set; }

        // One office has MANY divisions
        public virtual ICollection<Division> Divisions { get; set; } = new List<Division>();

        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }

    // ── DIVISION ──────────────────────────────────────────────────
    // Maps to the 'division' table
    // Third level — belongs to an Office
    // e.g. "Budget Division"
    [Table("division")]
    public class Division
    {
        [Key]
        [Column("division_id")]
        public int DivisionId { get; set; }

        [Required]
        [Column("division_name")]
        [MaxLength(255)]
        public string DivisionName { get; set; } = string.Empty;

        [Column("division_code")]
        [MaxLength(50)]
        public string? DivisionCode { get; set; }

        // Foreign key — which office this division belongs to
        [Column("office_id")]
        public int OfficeId { get; set; }

        [ForeignKey("OfficeId")]
        public virtual Office? Office { get; set; }

        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }
}