using LDHub.Data;
using LDHub.Helpers;
using LDHub.Models;
using LDHub.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LDHub.Tests;

public class NotificationAccessTests
{
    // Region 10 has training 1, region 20 has training 2.
    //   user 1 = RAAM in region 10      user 2 = RAAM with no region
    //   user 3 = participant in training 1      user 4 = resource person for training 1
    private static AppDbContext Seed()
    {
        var db = TestDb.Create();
        db.Users.Add(TestDb.NewUser(1, regionId: 10));
        db.Users.Add(TestDb.NewUser(2));
        db.Users.Add(TestDb.NewUser(3, regionId: 10));
        db.Users.Add(TestDb.NewUser(4, regionId: 10));
        db.Trainings.Add(TestDb.NewTraining(1, regionId: 10));
        db.Trainings.Add(TestDb.NewTraining(2, regionId: 20));
        db.TrainingParticipants.Add(new TrainingParticipant { TrainingId = 1, UserId = 3, CompletionStatus = EnrollmentStatus.Enrolled });
        db.TrainingInstructors.Add(new TrainingInstructor { TrainingId = 1, UserId = 4 });

        foreach (var userId in new[] { 1, 2, 3, 4 })
        {
            db.Notifications.Add(new Notification { UserId = userId, Title = "No training" });
            db.Notifications.Add(new Notification { UserId = userId, Title = "Training 1", TrainingId = 1 });
            db.Notifications.Add(new Notification { UserId = userId, Title = "Training 2", TrainingId = 2 });
        }

        db.SaveChanges();
        return db;
    }

    private static async Task<string[]> TitlesFor(AppDbContext db, int userId, string role)
    {
        var visible = await db.VisibleNotificationsAsync(userId, role);
        return (await visible.OrderBy(n => n.Title).ToListAsync()).Select(n => n.Title).ToArray();
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.CourseManager)]
    public async Task Admin_and_course_managers_see_every_notification(string role)
    {
        using var db = Seed();

        Assert.Equal(new[] { "No training", "Training 1", "Training 2" }, await TitlesFor(db, 1, role));
    }

    [Fact]
    public async Task A_raam_only_sees_trainings_in_their_own_region()
    {
        using var db = Seed();

        // user 1 also has a notification for training 2 (region 20), but is acting as RAAM of region 10
        Assert.Equal(new[] { "No training", "Training 1" }, await TitlesFor(db, 1, AppRoles.Raam));
    }

    [Fact]
    public async Task A_raam_with_no_region_only_sees_notifications_that_are_not_about_a_training()
    {
        using var db = Seed();

        Assert.Equal(new[] { "No training" }, await TitlesFor(db, 2, AppRoles.Raam));
    }

    [Fact]
    public async Task A_participant_only_sees_trainings_they_are_enrolled_in()
    {
        using var db = Seed();

        Assert.Equal(new[] { "No training", "Training 1" }, await TitlesFor(db, 3, AppRoles.Employee));
    }

    [Fact]
    public async Task A_resource_person_only_sees_trainings_they_are_assigned_to()
    {
        using var db = Seed();

        Assert.Equal(new[] { "No training", "Training 1" }, await TitlesFor(db, 4, AppRoles.ResourcePerson));
    }

    [Fact]
    public async Task The_same_person_sees_different_notifications_in_a_different_role()
    {
        using var db = Seed();

        // user 1 is a RAAM, but is not enrolled in anything as a participant
        Assert.Equal(new[] { "No training" }, await TitlesFor(db, 1, AppRoles.Employee));
        Assert.Equal(new[] { "No training", "Training 1" }, await TitlesFor(db, 1, AppRoles.Raam));
    }

    [Fact]
    public async Task Nobody_sees_another_users_notifications()
    {
        using var db = Seed();

        var visible = await db.VisibleNotificationsAsync(3, AppRoles.Admin);

        Assert.All(await visible.ToListAsync(), n => Assert.Equal(3, n.UserId));
    }

    [Fact]
    public async Task The_training_is_saved_with_the_notification()
    {
        using var db = TestDb.Create();
        var service = new NotificationService(db, NullLogger<NotificationService>.Instance);

        await service.NotifyAsync(new[] { 5 }, "Enrolled", "msg", "/x", trainingId: 7);
        await service.NotifyAsync(new[] { 5 }, "Welcome", null, null);

        var rows = await db.Notifications.OrderBy(n => n.Title).ToListAsync();
        Assert.Equal(7, rows[0].TrainingId);      // "Enrolled"
        Assert.Null(rows[1].TrainingId);          // "Welcome"
    }
}
