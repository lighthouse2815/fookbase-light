using System.Net.Mail;

namespace Fookbase.Api.Modules.Identity.Config;

public sealed class EmailOptions
{
    public bool Enabled { get; init; }

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 587;

    public bool UseSsl { get; init; } = true;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = "Fookbase";

    public string FrontendBaseUrl { get; init; } = "http://localhost:5173";

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Host) || Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) ||
            string.IsNullOrWhiteSpace(FrontendBaseUrl))
        {
            throw new InvalidOperationException("SMTP email configuration is incomplete.");
        }

        try
        {
            _ = new MailAddress(FromAddress);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Email:FromAddress must be a valid email address.", exception);
        }
    }
}
