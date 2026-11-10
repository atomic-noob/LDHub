using LDHub.Helpers;

namespace LDHub.Tests;

public class TrainingAccessTests
{
    private static async Task<bool> CanManage(string? role, int? userId, int trainingId, Action<LDHub.Data.AppDbContext>? seed = null)
    {
        using var db = TestDb.Create();
        db.Users.Add(TestDb.NewUser(1, regionId: 10));     // RAAM in region 10
        db.Users.Add(TestDb.NewUser(2));                   // RAAM with no region
        db.Trainings.Add(TestDb.NewTraining(1, regionId: 10));
        db.Trainings.Add(TestDb.NewTraining(2, regionId: 20));
        db.Trainings.Add(TestDb.NewTraining(3, regionId: null));
        seed?.Invoke(db);
        await db.SaveChangesAsync();

        var ctx = TestHttp.Context(role, userId);
        return await db.CanManageTrainingAsync(ctx.Session, trainingId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Admin_and_course_managers_can_manage_any_training(int trainingId)
    {
        Assert.True(await CanManage(AppRoles.Admin, 99, trainingId));
        Assert.True(await CanManage(AppRoles.CourseManager, 99, trainingId));
    }

    [Fact]
    public async Task A_raam_can_manage_a_training_in_their_own_region()
    {
        Assert.True(await CanManage(AppRoles.Raam, 1, 1));
    }

    [Fact]
    public async Task A_raam_cannot_manage_a_training_in_another_region()
    {
        Assert.False(await CanManage(AppRoles.Raam, 1, 2));
    }

    [Fact]
    public async Task A_raam_cannot_manage_a_training_with_no_region()
    {
        Assert.False(await CanManage(AppRoles.Raam, 1, 3));
    }

    [Fact]
    public async Task A_raam_with_no_region_cannot_manage_a_training_with_no_region()
    {
        Assert.False(await CanManage(AppRoles.Raam, 2, 3));
    }

    [Fact]
    public async Task A_raam_who_is_not_logged_in_gets_nothing()
    {
        Assert.False(await CanManage(AppRoles.Raam, null, 1));
    }

    [Theory]
    [InlineData(AppRoles.Employee)]
    [InlineData(AppRoles.ResourcePerson)]
    [InlineData(null)]
    public async Task Everyone_else_cannot_manage_trainings(string? role)
    {
        Assert.False(await CanManage(role, 1, 1));
    }
}
