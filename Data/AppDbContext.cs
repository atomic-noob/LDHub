using Microsoft.EntityFrameworkCore;
using LDHub.Models;

namespace LDHub.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // ── Tables ────────────────────────────────────────────────

        // User tables
        public DbSet<User> Users { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<PendingParticipant> PendingParticipants { get; set; }
        public DbSet<Training> Trainings { get; set; }
        

        // Location tables
        public DbSet<Region> Regions { get; set; }
        public DbSet<Office> Offices { get; set; }
        public DbSet<Division> Divisions { get; set; }

        // ── Relationships & Constraints ───────────────────────────
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // One user can have many roles
            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Email must be unique
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Pending email must be unique
            modelBuilder.Entity<PendingParticipant>()
                .HasIndex(p => p.Email)
                .IsUnique();

            // User → Region
            modelBuilder.Entity<User>()
                .HasOne(u => u.Region)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RegionId)
                .OnDelete(DeleteBehavior.SetNull);

            // User → Office
            modelBuilder.Entity<User>()
                .HasOne(u => u.Office)
                .WithMany(o => o.Users)
                .HasForeignKey(u => u.OfficeId)
                .OnDelete(DeleteBehavior.SetNull);

            // User → Division
            modelBuilder.Entity<User>()
                .HasOne(u => u.Division)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DivisionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Region → Office
            modelBuilder.Entity<Office>()
                .HasOne(o => o.Region)
                .WithMany(r => r.Offices)
                .HasForeignKey(o => o.RegionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Office → Division
            modelBuilder.Entity<Division>()
                .HasOne(d => d.Office)
                .WithMany(o => o.Divisions)
                .HasForeignKey(d => d.OfficeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}