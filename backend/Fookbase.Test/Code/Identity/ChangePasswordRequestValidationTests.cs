using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class ChangePasswordRequestValidationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Password_confirmation_is_validated_by_mvc(bool reset, bool matching)
    {
        const string password = "New-password-123!";
        var confirmation = matching ? password : "different-password";
        object request = reset
            ? new ResetPasswordRequest("user@example.test", "reset-token", password, confirmation)
            : new ChangePasswordRequest("Current-password-123!", password, confirmation);
        var serviceCollection = new ServiceCollection().AddLogging();
        serviceCollection.AddControllers();
        using var services = serviceCollection.BuildServiceProvider();
        var context = new ActionContext(
            new DefaultHttpContext { RequestServices = services },
            new RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor(),
            new ModelStateDictionary());

        services.GetRequiredService<IObjectModelValidator>().Validate(context, null, string.Empty, request);

        Assert.Equal(matching, context.ModelState.IsValid);
        if (!matching)
        {
            var error = Assert.Single(context.ModelState[nameof(ChangePasswordRequest.ConfirmPassword)]!.Errors);
            Assert.Equal("Xác nhận mật khẩu không khớp.", error.ErrorMessage);
        }
    }
}
