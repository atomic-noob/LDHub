using LDHub.Data;
using LDHub.Helpers;
using LDHub.Models;
using Microsoft.AspNetCore.Http;

namespace LDHub.Tests;

public class PrivateUploadsMiddlewareTests
{
    private const string ModuleFile      = "/uploads/modules/abc_lecture.pdf";
    private const string CertificateFile = "/uploads/certificates/cert_1.pdf";

    private static AppDbContext Seed(Action<AppDbContext>? more = null)
    {
        var db = TestDb.Create();
        db.Users.Add(TestDb.NewUser(5, regionId: 10));
        db.Users.Add(TestDb.NewUser(6, regionId: 10));
        db.Trainings.Add(new Training
        {
            TrainingId = 1, TrainingCode = "T1", TrainingTitle = "T1",
            RegionId = 10, CertificatePath = CertificateFile
        });
        db.TrainingModules.Add(TestDb.NewModule(1, 1, ModuleFile));
        db.TrainingParticipants.Add(new TrainingParticipant { TrainingId = 1, UserId = 5, CompletionStatus = "Enrolled" });
        more?.Invoke(db);
        db.SaveChanges();
        return db;
    }

    private static async Task<(int status, bool reachedFile)> Request(
        AppDbContext db, string path, string? role, int? userId)
    {
        var ctx = TestHttp.Context(role, userId);
        ctx.Request.Path = path;

        var reached = false;
        var middleware = new PrivateUploadsMiddleware(_ => { reached = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(ctx, db);

        return (ctx.Response.StatusCode, reached);
    }

    [Fact]
    public async Task Public_pages_and_public_images_are_not_touched()
    {
        using var db = Seed();

        Assert.True((await Request(db, "/css/site.css", null, null)).reachedFile);
        Assert.True((await Request(db, "/uploads/faqs/picture.png", null, null)).reachedFile);
        Assert.True((await Request(db, "/uploads/free-trainings/photo.jpg", null, null)).reachedFile);
    }

    [Theory]
    [InlineData(ModuleFile)]
    [InlineData(CertificateFile)]
    [InlineData("/uploads/profile-photos/me.jpg")]
    public async Task Private_files_need_a_login(string path)
    {
        using var db = Seed();

        var (status, reached) = await Request(db, path, null, null);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.False(reached);
    }

    [Fact]
    public async Task An_enrolled_participant_can_open_the_training_material_and_certificate()
    {
        using var db = Seed();

        Assert.True((await Request(db, ModuleFile, AppRoles.Employee, 5)).reachedFile);
        Assert.True((await Request(db, CertificateFile, AppRoles.Employee, 5)).reachedFile);
    }

    [Fact]
    public async Task A_participant_who_is_not_enrolled_gets_a_403()
    {
        using var db = Seed();

        var (status, reached) = await Request(db, ModuleFile, AppRoles.Employee, 6);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.False(reached);
    }

    [Fact]
    public async Task A_file_that_belongs_to_no_training_is_refused_to_ordinary_users()
    {
        using var db = Seed();

        var (status, reached) = await Request(db, "/uploads/modules/unknown.pdf", AppRoles.Employee, 5);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.False(reached);
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.CourseManager)]
    public async Task Admin_and_course_managers_can_open_any_file(string role)
    {
        using var db = Seed();

        Assert.True((await Request(db, ModuleFile, role, 99)).reachedFile);
        Assert.True((await Request(db, "/uploads/modules/unknown.pdf", role, 99)).reachedFile);
    }

    [Fact]
    public async Task A_resource_person_can_open_files_only_for_trainings_they_are_assigned_to()
    {
        using var db = Seed(d => d.TrainingInstructors.Add(new TrainingInstructor { TrainingId = 1, UserId = 6 }));

        Assert.True((await Request(db, ModuleFile, AppRoles.ResourcePerson, 6)).reachedFile);
        Assert.Equal(StatusCodes.Status403Forbidden, (await Request(db, ModuleFile, AppRoles.ResourcePerson, 99)).status);
    }

    [Fact]
    public async Task A_raam_can_open_files_only_for_trainings_in_their_region()
    {
        using var db = Seed(d =>
        {
            d.Users.Add(TestDb.NewUser(7, regionId: 10));   // same region as the training
            d.Users.Add(TestDb.NewUser(8, regionId: 20));   // another region
        });

        Assert.True((await Request(db, ModuleFile, AppRoles.Raam, 7)).reachedFile);
        Assert.Equal(StatusCodes.Status403Forbidden, (await Request(db, ModuleFile, AppRoles.Raam, 8)).status);
    }

    [Fact]
    public async Task Any_logged_in_user_can_see_profile_photos()
    {
        using var db = Seed();

        Assert.True((await Request(db, "/uploads/profile-photos/me.jpg", AppRoles.Employee, 6)).reachedFile);
    }
}
