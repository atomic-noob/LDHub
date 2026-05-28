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
        public DbSet<TrainingParticipant> TrainingParticipants { get; set; }
        public DbSet<TrainingInstructor> TrainingInstructors { get; set; }
        

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
            
            // Training → Participants
modelBuilder.Entity<TrainingParticipant>()
    .HasKey(tp => new { tp.TrainingId, tp.UserId });

modelBuilder.Entity<TrainingParticipant>()
    .HasOne(tp => tp.Training)
    .WithMany(t => t.TrainingParticipants)
    .HasForeignKey(tp => tp.TrainingId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<TrainingParticipant>()
    .HasOne(tp => tp.User)
    .WithMany()
    .HasForeignKey(tp => tp.UserId)
    .OnDelete(DeleteBehavior.Cascade);

// Training → Instructors
modelBuilder.Entity<TrainingInstructor>()
    .HasKey(ti => new { ti.TrainingId, ti.UserId });

modelBuilder.Entity<TrainingInstructor>()
    .HasOne(ti => ti.Training)
    .WithMany(t => t.TrainingInstructors)
    .HasForeignKey(ti => ti.TrainingId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<TrainingInstructor>()
    .HasOne(ti => ti.User)
    .WithMany()
    .HasForeignKey(ti => ti.UserId)
    .OnDelete(DeleteBehavior.Cascade);
        }
    }
}