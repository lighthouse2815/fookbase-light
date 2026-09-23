using System.Text.Json;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fookbase.Api.IntegrationTests;

public sealed class BusinessExceptionHandlerTests
{
    [Theory]
    [InlineData(ApplicationErrorType.Validation, 400)]
    [InlineData(ApplicationErrorType.Unauthorized, 401)]
    [InlineData(ApplicationErrorType.Forbidden, 403)]
    [InlineData(ApplicationErrorType.NotFound, 404)]
    [InlineData(ApplicationErrorType.Conflict, 409)]
    public async Task Business_errors_preserve_the_existing_problem_contract(
        ApplicationErrorType type, int status)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "business-error-request" };
        context.Response.Body = body;
        var error = new ApplicationError("test_error", "Expected failure.", type,
            new Dictionary<string, string[]> { ["password"] = ["Password is required."] });
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        Assert.True(await handler.TryHandleAsync(context, new BusinessException(error), CancellationToken.None));

        Assert.Equal(status, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        var problem = document.RootElement;
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.Equal(type.ToString(), problem.GetProperty("title").GetString());
        Assert.Equal(error.Code, problem.GetProperty("code").GetString());
        Assert.Equal(error.Message, problem.GetProperty("detail").GetString());
        Assert.Equal("business-error-request", problem.GetProperty("requestId").GetString());
        Assert.Equal("Password is required.", problem.GetProperty("errors").GetProperty("password")[0].GetString());
    }
}
