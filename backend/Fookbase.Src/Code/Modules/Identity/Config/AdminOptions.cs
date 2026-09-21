using System.Net.Mail;

namespace Fookbase.Api.Modules.Identity.Config;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string? BootstrapEmail { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BootstrapEmail))
        {
            return;
        }

        try
        {
            _ = new MailAddress(BootstrapEmail);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Admin:BootstrapEmail must be a valid email address.", exception);
        }
    }

    public bool IsBootstrapAdmin(string email) =>
        !string.IsNullOrWhiteSpace(BootstrapEmail) &&
        string.Equals(BootstrapEmail.Trim(), email, StringComparison.OrdinalIgnoreCase);
}
