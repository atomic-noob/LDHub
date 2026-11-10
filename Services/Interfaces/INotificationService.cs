namespace LDHub.Services.Interfaces;

public interface INotificationService
{
    /// <summary>Creates one notification per user. Never throws; failures are logged.</summary>
    Task NotifyAsync(IEnumerable<int> userIds, string title, string? message, string? url);
}
