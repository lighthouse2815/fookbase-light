using System.Text.Json;

namespace Fookbase.Api.Modules.Messages.Common;

internal static class MessageCursorCodec
{
    public const int MaximumLength = 256;

    public static string Encode<T>(T cursor) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(cursor))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static T Decode<T>(string value) where T : class
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return JsonSerializer.Deserialize<T>(Convert.FromBase64String(base64))
            ?? throw new JsonException("The message cursor is invalid.");
    }

    public static bool TryDecode<T>(string value) where T : class
    {
        try
        {
            _ = Decode<T>(value);
            return true;
        }
        catch (ArgumentException) { return false; }
        catch (FormatException) { return false; }
        catch (JsonException) { return false; }
    }
}
