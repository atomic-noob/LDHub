using LDHub.Models;

namespace LDHub.Services.Interfaces;

public record EmailMessage(string ToEmail, string ToName, string Subject, string HtmlBody);

public interface IEmailService
{
    /// <summary>Sends the messages over one SMTP connection. Never throws; returns how many were sent.</summary>
    Task<int> SendManyAsync(IEnumerable<EmailMessage> messages);

    /// <summary>Notify participants that they were enrolled in a training.</summary>
    Task SendTrainingAssignedAsync(IEnumerable<User> participants, Training training);

    /// <summary>Notify resource persons (SMEs / instructors) that they were assigned to a training.</summary>
    Task SendInstructorAssignedAsync(IEnumerable<User> instructors, Training training);

    /// <summary>Send a staff announcement for a training to everyone in its group.</summary>
    Task SendAnnouncementAsync(IEnumerable<User> recipients, Training training, string title, string body);

    /// <summary>Tell a newly registered participant that their account was approved.</summary>
    Task SendRegistrationApprovedAsync(string email, string fullName);

    /// <summary>Tell a registrant that their registration was not approved.</summary>
    Task SendRegistrationRejectedAsync(string email, string fullName, string? reason);
}
