using System.Text.Json;
using Fookbase.Api.Modules.Notifications.Config;
using Fookbase.Api.Modules.Notifications.DTOs.Requests;
using Fookbase.Api.Modules.Notifications.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Notifications.Services;

public sealed class PushNotificationService(
    FookbaseDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    PushNotificationOptions options,
    TimeProvider timeProvider,
    ILogger<PushNotificationService> logger)
{
    public async Task RegisterZolaDeviceAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var device = await dbContext.PushDevices.SingleOrDefaultAsync(
            item => item.ExpoPushToken == token,
            cancellationToken);
        if (device is null)
        {
            dbContext.PushDevices.Add(PushDevice.Create(Guid.NewGuid(), userId, token, now));
        }
        else
        {
            device.Register(userId, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UnregisterZolaDeviceAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var device = await dbContext.PushDevices.SingleOrDefaultAsync(
            item => item.UserId == userId && item.ExpoPushToken == token,
            cancellationToken);
        if (device is null)
        {
            return;
        }

        device.Disable(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SendMessageAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || recipientUserIds.Count == 0)
        {
            return;
        }

        try
        {
            var devices = await dbContext.PushDevices
                .Where(device => recipientUserIds.Contains(device.UserId) && device.DisabledAtUtc == null)
                .ToListAsync(cancellationToken);
            if (devices.Count == 0)
            {
                return;
            }

            var messages = devices.Select(device => new ExpoPushMessage(
                device.ExpoPushToken,
                "Zola",
                "Bạn có một tin nhắn mới.",
                "default",
                new { conversationId })).ToArray();
            using var response = await httpClientFactory.CreateClient("expo-push")
                .PostAsJsonAsync(options.ExpoPushApiUrl, messages, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Expo push request failed with status {StatusCode}.", response.StatusCode);
                return;
            }

            using var payload = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            if (!payload.RootElement.TryGetProperty("data", out var tickets) || tickets.ValueKind != JsonValueKind.Array)
            {
                logger.LogWarning("Expo push response did not include ticket data.");
                return;
            }

            var now = timeProvider.GetUtcNow();
            foreach (var (device, ticket) in devices.Zip(tickets.EnumerateArray()))
            {
                if (HasError(ticket, "DeviceNotRegistered"))
                {
                    device.Disable(now);
                    continue;
                }

                if (ticket.TryGetProperty("status", out var status) && status.GetString() == "ok" &&
                    ticket.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(id.GetString()))
                {
                    dbContext.PushDeliveryReceipts.Add(PushDeliveryReceipt.Create(
                        Guid.NewGuid(), device.Id, id.GetString()!, now.AddMinutes(15)));
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or DbUpdateException)
        {
            logger.LogWarning(exception, "Expo push delivery failed; persisted messages remain available to the recipient.");
        }
    }

    public async Task CheckReceiptsAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            return;
        }

        try
        {
            var now = timeProvider.GetUtcNow();
            var receipts = await dbContext.PushDeliveryReceipts
                .Where(receipt => receipt.CheckedAtUtc == null && receipt.AvailableAtUtc <= now)
                .OrderBy(receipt => receipt.AvailableAtUtc)
                .Take(1_000)
                .ToListAsync(cancellationToken);
            if (receipts.Count == 0)
            {
                return;
            }

            using var response = await httpClientFactory.CreateClient("expo-push")
                .PostAsJsonAsync(options.ExpoPushReceiptsUrl,
                    new { ids = receipts.Select(receipt => receipt.ExpoReceiptId).ToArray() }, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Expo receipt request failed with status {StatusCode}.", response.StatusCode);
                return;
            }

            using var payload = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            if (!payload.RootElement.TryGetProperty("data", out var result) || result.ValueKind != JsonValueKind.Object)
            {
                logger.LogWarning("Expo receipt response did not include receipt data.");
                return;
            }

            var devices = await dbContext.PushDevices.Where(device =>
                    receipts.Select(receipt => receipt.PushDeviceId).Contains(device.Id))
                .ToDictionaryAsync(device => device.Id, cancellationToken);
            foreach (var receipt in receipts)
            {
                if (!result.TryGetProperty(receipt.ExpoReceiptId, out var item))
                {
                    if (receipt.CheckAttempts >= 12)
                    {
                        receipt.MarkChecked(now);
                    }
                    else
                    {
                        receipt.RetryAt(now.AddMinutes(5));
                    }
                    continue;
                }

                receipt.MarkChecked(now);
                if (HasError(item, "DeviceNotRegistered") && devices.TryGetValue(receipt.PushDeviceId, out var device))
                {
                    device.Disable(now);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or DbUpdateException)
        {
            logger.LogWarning(exception, "Expo push receipt check failed.");
        }
    }

    private static bool HasError(JsonElement item, string expected) =>
        item.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object &&
        details.TryGetProperty("error", out var error) && error.GetString() == expected;
}
