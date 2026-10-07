using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;

namespace Fookbase.Api.Modules.Messages.Common;

internal static class MessageCursorCodec
{
    public const int MaximumLength = 256;

    public static string Encode<T>(T cursor) =>
        WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(cursor));

    public static T Decode<T>(string value) where T : class
    {
        return JsonSerializer.Deserialize<T>(WebEncoders.Base64UrlDecode(value))
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
