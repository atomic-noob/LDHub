namespace LDHub.Services;

/// <summary>Bound from the "EmailSettings" section of appsettings / environment variables.</summary>
public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "LDHub";

    /// <summary>Optional. Public site address, e.g. https://lndhub.example.gov.ph. Used for the login link in emails.</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
