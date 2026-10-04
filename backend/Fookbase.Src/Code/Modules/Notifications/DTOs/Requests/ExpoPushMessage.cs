namespace Fookbase.Api.Modules.Notifications.DTOs.Requests;

internal sealed record ExpoPushMessage(string To, string Title, string Body, string Sound, object Data);
