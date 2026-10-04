using Fookbase.Api.Modules.Identity.Entities;

namespace Fookbase.Identity.Api.IntegrationTests;

public class AuthSessionTests
{
    [Fact]
    public void Constructor_removes_line_breaks_from_user_agent()
    {
        var session = new AuthSession(
            Guid.NewGuid(),
            "Fookbase\nMobile",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(30));

        Assert.Equal("FookbaseMobile", session.UserAgent);
    }
}
