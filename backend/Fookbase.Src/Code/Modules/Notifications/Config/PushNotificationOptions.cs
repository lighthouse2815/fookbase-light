namespace Fookbase.Api.Modules.Notifications.Config;

public sealed class PushNotificationOptions
{
    public const string SectionName = "PushNotifications";

    public bool Enabled { get; init; }

    public string ExpoPushApiUrl { get; init; } = "https://exp.host/--/api/v2/push/send";

    public string ExpoPushReceiptsUrl { get; init; } = "https://exp.host/--/api/v2/push/getReceipts";

    public int ReceiptCheckIntervalMinutes { get; init; } = 15;

    public void Validate()
    {
        if (ReceiptCheckIntervalMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException("PushNotifications:ReceiptCheckIntervalMinutes must be between 1 and 60.");
        }

        ValidateUrl(ExpoPushApiUrl, "PushNotifications:ExpoPushApiUrl");
        ValidateUrl(ExpoPushReceiptsUrl, "PushNotifications:ExpoPushReceiptsUrl");
    }

    private static void ValidateUrl(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException($"{name} must be an explicit HTTPS URL.");
        }
    }
}
