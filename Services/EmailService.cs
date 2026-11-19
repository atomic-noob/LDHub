using System.Net;
using LDHub.Models;
using LDHub.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LDHub.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.SmtpHost) &&
        !string.IsNullOrWhiteSpace(_settings.SmtpUsername) &&
        !string.IsNullOrWhiteSpace(_settings.SmtpPassword) &&
        !string.IsNullOrWhiteSpace(_settings.FromEmail);

    // ── Core sender ───────────────────────────────────────────────
    public async Task<int> SendManyAsync(IEnumerable<EmailMessage> messages)
    {
        var list = messages.Where(m => !string.IsNullOrWhiteSpace(m.ToEmail)).ToList();
        if (list.Count == 0) return 0;

        if (!IsConfigured)
        {
            _logger.LogWarning(
                "Email is not configured (EmailSettings). Skipped {Count} message(s).", list.Count);
            return 0;
        }

        var sent = 0;
        try
        {
            using var client = new SmtpClient();
            var security = _settings.SmtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, security);
            await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword);

            foreach (var m in list)
            {
                try
                {
                    await client.SendAsync(Build(m));
                    sent++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email to {To}", m.ToEmail);
                }
            }

            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            // A mail problem must never break the page the user is on.
            _logger.LogError(ex, "Could not connect or authenticate with the SMTP server.");
        }

        return sent;
    }

    private MimeMessage Build(EmailMessage m)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(new MailboxAddress(string.IsNullOrWhiteSpace(m.ToName) ? m.ToEmail : m.ToName, m.ToEmail));
        message.Subject = m.Subject;
        message.Body = new BodyBuilder { HtmlBody = m.HtmlBody }.ToMessageBody();
        return message;
    }

    // ── Notifications ─────────────────────────────────────────────
    public Task SendTrainingAssignedAsync(IEnumerable<User> participants, Training training)
    {
        var messages = participants.Select(u => new EmailMessage(
            u.Email,
            FullName(u),
            "You have been enrolled in: " + training.TrainingTitle,
            Wrap("Training Enrollment",
                Greeting(FullName(u)) +
                "<p>You have been enrolled in the following training:</p>" +
                TrainingDetails(training) +
                "<p>Please log in to the L&amp;D Hub to take the pre-test and access your modules once the training starts.</p>" +
                LoginLink())));

        return SendManyAsync(messages);
    }

    public Task SendInstructorAssignedAsync(IEnumerable<User> instructors, Training training)
    {
        var messages = instructors.Select(u => new EmailMessage(
            u.Email,
            FullName(u),
            "You have been assigned as Resource Person: " + training.TrainingTitle,
            Wrap("Resource Person Assignment",
                Greeting(FullName(u)) +
                "<p>You have been assigned as a Resource Person for the following training:</p>" +
                TrainingDetails(training) +
                "<p>Please log in to the L&amp;D Hub to prepare your modules and tests.</p>" +
                LoginLink())));

        return SendManyAsync(messages);
    }

    public Task SendAnnouncementAsync(IEnumerable<User> recipients, Training training, string title, string body)
    {
        var bodyHtml = H(body).Replace("\r\n", "<br>").Replace("\n", "<br>");

        var messages = recipients.Select(u => new EmailMessage(
            u.Email,
            FullName(u),
            "Announcement: " + title,
            Wrap("Announcement",
                Greeting(FullName(u)) +
                "<p><strong>" + H(title) + "</strong><br>" +
                "<span style=\"color:#666\">" + H(training.TrainingTitle) + "</span></p>" +
                "<p>" + bodyHtml + "</p>" +
                LoginLink())));

        return SendManyAsync(messages);
    }

    public Task SendRegistrationApprovedAsync(string email, string fullName) =>
        SendManyAsync(new[]
        {
            new EmailMessage(email, fullName,
                "Your L&D Hub registration has been approved",
                Wrap("Registration Approved",
                    Greeting(fullName) +
                    "<p>Your registration has been approved. You can now log in to the L&amp;D Hub using your registered email address.</p>" +
                    LoginLink()))
        });

    public Task SendRegistrationRejectedAsync(string email, string fullName, string? reason)
    {
        var reasonHtml = string.IsNullOrWhiteSpace(reason)
            ? string.Empty
            : "<p><strong>Reason:</strong> " + H(reason) + "</p>";

        return SendManyAsync(new[]
        {
            new EmailMessage(email, fullName,
                "Update on your L&D Hub registration",
                Wrap("Registration Update",
                    Greeting(fullName) +
                    "<p>We were unable to approve your registration at this time.</p>" +
                    reasonHtml +
                    "<p>If you believe this is a mistake, please contact the HRDS - Training and Development Division.</p>"))
        });
    }

    // ── Helpers ───────────────────────────────────────────────────
    private static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    private static string FullName(User u) => (u.FirstName + " " + u.LastName).Trim();

    private static string Greeting(string name) => "<p>Dear " + H(name) + ",</p>";

    private static string FormatDate(DateTime? d) =>
        d.HasValue ? d.Value.ToString("MMMM d, yyyy") : "To be announced";

    private static string Row(string label, string value) =>
        "<tr><td style=\"padding:6px 12px;color:#666;white-space:nowrap\">" + label +
        "</td><td style=\"padding:6px 12px;font-weight:bold\">" + value + "</td></tr>";

    private static string TrainingDetails(Training t)
    {
        var dates = t.StartDate.HasValue && t.EndDate.HasValue && t.StartDate.Value.Date != t.EndDate.Value.Date
            ? FormatDate(t.StartDate) + " to " + FormatDate(t.EndDate)
            : FormatDate(t.StartDate);

        var rows =
            Row("Training", H(t.TrainingTitle)) +
            Row("Code", H(t.TrainingCode)) +
            Row("Date", H(dates)) +
            Row("Location", H(string.IsNullOrWhiteSpace(t.Location) ? "To be announced" : t.Location));

        if (t.TrainingHrs.HasValue)
            rows += Row("Hours", t.TrainingHrs.Value.ToString());

        return "<table style=\"border-collapse:collapse;background:#f7f9fb;margin:8px 0\">" + rows + "</table>";
    }

    private string LoginLink()
    {
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl)) return string.Empty;
        var url = _settings.BaseUrl.TrimEnd('/') + "/Home/Login";
        return "<p><a href=\"" + H(url) + "\" style=\"background:#2e7d32;color:#fff;padding:10px 18px;" +
               "text-decoration:none;border-radius:4px;display:inline-block\">Log in to L&amp;D Hub</a></p>";
    }

    private static string Wrap(string heading, string inner) =>
        "<div style=\"font-family:Arial,sans-serif;max-width:600px;margin:auto;border:1px solid #e0e0e0\">" +
        "<div style=\"background:#2e7d32;color:#fff;padding:16px 20px\">" +
        "<div style=\"font-size:12px;opacity:.85\">DENR Learning and Development Hub</div>" +
        "<div style=\"font-size:20px;font-weight:bold\">" + H(heading) + "</div></div>" +
        "<div style=\"padding:20px;color:#222;font-size:14px;line-height:1.5\">" + inner + "</div>" +
        "<div style=\"padding:12px 20px;background:#f2f2f2;color:#777;font-size:12px\">" +
        "This is an automated message from the DENR L&amp;D Hub. Please do not reply.</div></div>";
}
