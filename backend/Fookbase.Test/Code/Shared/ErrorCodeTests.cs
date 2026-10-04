using Fookbase.Api.Shared.ErrorHandling;

namespace Fookbase.Api.IntegrationTests;

public sealed class ErrorCodeTests
{
    [Fact]
    public void Validation_failed_exposes_a_stable_code_and_default_message()
    {
        Assert.Equal("validation_failed", ErrorCode.ValidationFailed.Code);
        Assert.Equal("One or more validation errors occurred.", ErrorCode.ValidationFailed.Message);
    }
}
