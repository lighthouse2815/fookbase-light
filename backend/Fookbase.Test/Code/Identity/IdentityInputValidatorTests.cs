using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class IdentityInputValidatorTests
{
    [Fact]
    public void Is_valid_sha256_hex_accepts_only_a_sha256_hex_digest()
    {
        Assert.True(IdentityInputValidator.IsValidSha256Hex(new string('A', 64)));
        Assert.False(IdentityInputValidator.IsValidSha256Hex("not-a-digest"));
    }

    [Fact]
    public void Validate_sha256_hex_rejects_a_non_digest_value()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            IdentityInputValidator.ValidateSha256Hex("not-a-digest", "Mã băm của mã hoàn tất", "codeHash"));

        Assert.Equal("codeHash", exception.ParamName);
        Assert.StartsWith("Mã băm của mã hoàn tất phải là chuỗi SHA-256 dạng thập lục phân.", exception.Message);
    }

    [Fact]
    public void Validate_optional_external_login_allows_a_complete_or_empty_login()
    {
        IdentityInputValidator.ValidateOptionalExternalLogin(null, null);
        IdentityInputValidator.ValidateOptionalExternalLogin("Google", "google-subject");
    }

    [Fact]
    public void Validate_optional_external_login_rejects_an_incomplete_or_oversized_login()
    {
        Assert.Throws<ArgumentException>(() => IdentityInputValidator.ValidateOptionalExternalLogin("Google", null));
        Assert.Throws<ArgumentException>(() => IdentityInputValidator.ValidateOptionalExternalLogin(
            new string('G', 33),
            "google-subject"));
    }
}
