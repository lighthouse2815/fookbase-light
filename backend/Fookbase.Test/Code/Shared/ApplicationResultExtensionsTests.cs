using System.Text.Json;
using Fookbase.Api.Shared.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Api.IntegrationTests;

public sealed class ApplicationResultExtensionsTests
{
    [Theory]
    [InlineData(ApplicationErrorType.VALIDATION, 400, "Validation")]
    [InlineData(ApplicationErrorType.UNAUTHORIZED, 401, "Unauthorized")]
    [InlineData(ApplicationErrorType.FORBIDDEN, 403, "Forbidden")]
    [InlineData(ApplicationErrorType.NOT_FOUND, 404, "NotFound")]
    [InlineData(ApplicationErrorType.CONFLICT, 409, "Conflict")]
    [InlineData((ApplicationErrorType)999, 500, "999")]
    public async Task Error_response_preserves_status_code_and_problem_fields(
        ApplicationErrorType type, int statusCode, string title)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = body;
        var error = new ApplicationError("example_error", "The operation failed.", type);

        await error.ToHttpResult().ExecuteAsync(context);

        Assert.Equal(statusCode, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        var problem = document.RootElement;
        Assert.Equal(statusCode, problem.GetProperty("status").GetInt32());
        Assert.Equal(title, problem.GetProperty("title").GetString());
        Assert.Equal(error.Code, problem.GetProperty("code").GetString());
        Assert.Equal(error.Message, problem.GetProperty("detail").GetString());
        Assert.False(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Validation_response_preserves_field_errors()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = body;
        var error = new ApplicationError(
            "validation_failed",
            "One or more validation errors occurred.",
            ApplicationErrorType.VALIDATION,
            new Dictionary<string, string[]> { ["email"] = ["Email is required.", "Email is invalid."] });

        await error.ToHttpResult().ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        Assert.Equal(
            new[] { "Email is required.", "Email is invalid." },
            document.RootElement.GetProperty("errors").GetProperty("email")
                .EnumerateArray().Select(message => message.GetString()));
    }
}
