using LDHub.Helpers;
using LDHub.Models;
using LDHub.Services;
using Microsoft.EntityFrameworkCore;

namespace LDHub.Tests;

public class UserApprovalServiceTests
{
    private static PendingParticipant Pending(string email = "new.person@example.com") => new()
    {
        ParticipantId = 1,
        FirstName = "New",
        LastName  = "Person",
        Email     = email,
        Password  = "hashed",
        Gender    = "F",
        Position  = "Analyst",
        Status    = RecordStatus.Inactive,
        RegionId  = 10
    };

    [Fact]
    public async Task Approving_creates_an_active_employee_and_emails_them()
    {
        using var db = TestDb.Create();
        var pending = Pending();
        db.PendingParticipants.Add(pending);
        await db.SaveChangesAsync();
        var email = new FakeEmailService();

        var error = await new UserApprovalService(db, email).ApproveAsync(pending, removePending: true);

        Assert.Null(error);
        var user = await db.Users.SingleAsync();
        Assert.Equal("new.person@example.com", user.Email);
        Assert.Equal(RecordStatus.Active, user.Status);
        Assert.Equal(10, user.RegionId);
        Assert.Equal(AppRoles.Employee, (await db.UserRoles.SingleAsync()).Role);
        Assert.Equal(new[] { "new.person@example.com" }, email.ApprovedEmails);
    }

    [Fact]
    public async Task The_admin_flow_removes_the_pending_row()
    {
        using var db = TestDb.Create();
        var pending = Pending();
        db.PendingParticipants.Add(pending);
        await db.SaveChangesAsync();

        await new UserApprovalService(db, new FakeEmailService()).ApproveAsync(pending, removePending: true);

        Assert.Empty(db.PendingParticipants);
    }

    [Fact]
    public async Task The_raam_flow_keeps_the_pending_row_and_marks_it_active()
    {
        using var db = TestDb.Create();
        var pending = Pending();
        db.PendingParticipants.Add(pending);
        await db.SaveChangesAsync();

        await new UserApprovalService(db, new FakeEmailService()).ApproveAsync(pending, removePending: false);

        var kept = await db.PendingParticipants.SingleAsync();
        Assert.Equal(RecordStatus.Active, kept.Status);
    }

    [Fact]
    public async Task An_email_that_already_has_an_account_is_refused()
    {
        using var db = TestDb.Create();
        db.Users.Add(TestDb.NewUser(1, email: "new.person@example.com"));
        var pending = Pending();
        db.PendingParticipants.Add(pending);
        await db.SaveChangesAsync();
        var email = new FakeEmailService();

        var error = await new UserApprovalService(db, email).ApproveAsync(pending, removePending: true);

        Assert.NotNull(error);
        Assert.Contains("already exists", error);
        Assert.Equal(1, await db.Users.CountAsync());          // no second account
        Assert.Empty(email.ApprovedEmails);                    // and no email
        Assert.Single(db.PendingParticipants);                 // the request is still waiting
    }
}
