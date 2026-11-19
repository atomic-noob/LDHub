using LDHub.Services;
using LDHub.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LDHub.Tests;

public class NotificationAndEmailServiceTests
{
    [Fact]
    public async Task One_notification_is_created_for_each_distinct_user()
    {
        using var db = TestDb.Create();
        var service = new NotificationService(db, NullLogger<NotificationService>.Instance);

        await service.NotifyAsync(new[] { 1, 1, 2 }, "Enrolled", "You were enrolled.", "/Employee/Dashboard");

        var rows = await db.Notifications.OrderBy(n => n.UserId).ToListAsync();
        Assert.Equal(new[] { 1, 2 }, rows.Select(n => n.UserId).ToArray());
        Assert.All(rows, n => Assert.False(n.IsRead));
    }

    [Fact]
    public async Task An_empty_list_creates_nothing()
    {
        using var db = TestDb.Create();
        var service = new NotificationService(db, NullLogger<NotificationService>.Instance);

        await service.NotifyAsync(Array.Empty<int>(), "Nothing", null, null);

        Assert.Empty(db.Notifications);
    }

    [Fact]
    public async Task Long_titles_and_messages_are_shortened_to_fit_the_columns()
    {
        using var db = TestDb.Create();
        var service = new NotificationService(db, NullLogger<NotificationService>.Instance);

        await service.NotifyAsync(new[] { 1 }, new string('t', 500), new string('m', 900), "/x");

        var row = await db.Notifications.SingleAsync();
        Assert.Equal(200, row.Title.Length);
        Assert.Equal(500, row.Message!.Length);
    }

    [Fact]
    public async Task Email_does_nothing_and_does_not_fail_when_it_is_not_configured()
    {
        var service = new EmailService(Options.Create(new EmailSettings()), NullLogger<EmailService>.Instance);

        var sent = await service.SendManyAsync(new[]
        {
            new EmailMessage("someone@example.com", "Someone", "Subject", "<p>Hello</p>")
        });

        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task Email_to_nobody_sends_nothing()
    {
        var service = new EmailService(Options.Create(new EmailSettings()), NullLogger<EmailService>.Instance);

        Assert.Equal(0, await service.SendManyAsync(Array.Empty<EmailMessage>()));
        Assert.Equal(0, await service.SendManyAsync(new[] { new EmailMessage("", "Nobody", "S", "B") }));
    }
}
