namespace LDHub.Services.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Creates one notification per user. Never throws; failures are logged.
    /// Pass trainingId for anything about a training, so it is only shown to roles that can reach that training.
    /// </summary>
    Task NotifyAsync(IEnumerable<int> userIds, string title, string? message, string? url, int? trainingId = null);
}
