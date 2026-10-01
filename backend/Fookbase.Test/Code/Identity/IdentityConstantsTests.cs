using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class IdentityConstantsTests
{
    [Fact]
    public void Keeps_identity_protocol_constant_values_in_one_place()
    {
        Assert.Equal("Admin", IdentityModuleConstants.Roles.Admin);
        Assert.Equal("Google", IdentityModuleConstants.ExternalLogin.GoogleScheme);
        Assert.Equal("GoogleExternal", IdentityModuleConstants.ExternalLogin.ExternalScheme);
        Assert.Equal("web", IdentityModuleConstants.ExternalLogin.Clients.Web);
        Assert.Equal("zola-light", IdentityModuleConstants.ExternalLogin.Clients.ZolaLight);
        Assert.Equal("mobile", IdentityModuleConstants.ExternalLogin.Clients.Mobile);
        Assert.Equal("zola-mobile", IdentityModuleConstants.ExternalLogin.Clients.ZolaMobile);
    }
}
