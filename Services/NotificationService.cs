using LDHub.Data;
using LDHub.Models;
using LDHub.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace LDHub.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(AppDbContext db, ILogger<NotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task NotifyAsync(IEnumerable<int> userIds, string title, string? message, string? url)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return;

        try
        {
            var now = DateTime.Now;
            foreach (var id in ids)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = id,
                    Title = Trim(title, 200),
                    Message = message == null ? null : Trim(message, 500),
                    Url = url == null ? null : Trim(url, 300),
                    IsRead = false,
                    CreatedAt = now
                });
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // A notification problem must never break the action that triggered it.
            _logger.LogError(ex, "Could not save notifications for {Count} user(s).", ids.Count);
        }
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
}
