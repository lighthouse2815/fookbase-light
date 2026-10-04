using System.Globalization;
using System.Security.Cryptography;

namespace Fookbase.Api.Modules.Identity.Common;

internal static class OtpCode
{
    public static string Create() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
}
