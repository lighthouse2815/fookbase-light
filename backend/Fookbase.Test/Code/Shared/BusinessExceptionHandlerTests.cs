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
    [InlineData(ApplicationErrorType.Validation, 400, false)]
    [InlineData(ApplicationErrorType.Unauthorized, 401, false)]
    [InlineData(ApplicationErrorType.Forbidden, 403, false)]
    [InlineData(ApplicationErrorType.NotFound, 404, false)]
    [InlineData(ApplicationErrorType.Conflict, 409, false)]
    [InlineData(ApplicationErrorType.Validation, 400, true)]
    [InlineData(ApplicationErrorType.Unauthorized, 401, true)]
    [InlineData(ApplicationErrorType.Forbidden, 403, true)]
    [InlineData(ApplicationErrorType.NotFound, 404, true)]
    [InlineData(ApplicationErrorType.Conflict, 409, true)]
    public async Task Business_errors_use_the_contract_for_the_requested_module(
        ApplicationErrorType type, int status, bool identity)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "business-error-request" };
        context.Request.Path = identity ? "/api/auth/login" : "/api/users/me";
        context.Response.Body = body;
        var error = new ApplicationError("test_error", "Expected failure.", type,
            new Dictionary<string, string[]> { ["password"] = ["Password is required."] });
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        Assert.True(await handler.TryHandleAsync(context, new BusinessException(error), CancellationToken.None));

        Assert.Equal(status, context.Response.StatusCode);
        Assert.StartsWith(identity ? "application/json" : "application/problem+json", context.Response.ContentType);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        var problem = document.RootElement;
        Assert.Equal("business-error-request", problem.GetProperty("requestId").GetString());
        if (identity)
        {
            Assert.False(problem.GetProperty("success").GetBoolean());
            Assert.Equal(JsonValueKind.Null, problem.GetProperty("data").ValueKind);
            var failure = problem.GetProperty("error");
            Assert.Equal(error.Code, failure.GetProperty("code").GetString());
            Assert.Equal(error.Message, failure.GetProperty("message").GetString());
            Assert.Equal("Password is required.", failure.GetProperty("details").GetProperty("password")[0].GetString());
            return;
        }

        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.Equal(type.ToString(), problem.GetProperty("title").GetString());
        Assert.Equal(error.Code, problem.GetProperty("code").GetString());
        Assert.Equal(error.Message, problem.GetProperty("detail").GetString());
        Assert.Equal("business-error-request", problem.GetProperty("requestId").GetString());
        Assert.Equal("Password is required.", problem.GetProperty("errors").GetProperty("password")[0].GetString());
    }

    [Theory]
    [InlineData(false, 500, "internal_server_error")]
    [InlineData(true, 400, "invalid_request")]
    public async Task Unexpected_and_malformed_request_errors_use_a_safe_envelope(bool invalidJson, int status, string code)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/api/auth/login";
        context.Response.Body = body;
        const string secret = "private database connection details";
        Exception exception = invalidJson ? new JsonException(secret) : new InvalidOperationException(secret);
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));

        Assert.Equal(status, context.Response.StatusCode);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(code, document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain(secret, document.RootElement.GetRawText());
    }
}
