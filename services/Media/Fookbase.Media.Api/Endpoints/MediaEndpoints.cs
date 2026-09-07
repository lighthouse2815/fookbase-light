using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fookbase.Media.Application.Media;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Media.Api.Endpoints;

public static class MediaEndpoints
{
    private const long MultipartBodyLengthLimit = MediaService.MaximumFileSize + 64 * 1024;

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/media");

        group.MapPost("", UploadAsync)
            .RequireAuthorization()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MultipartBodyLengthLimit));
        group.MapGet("/{mediaId:guid}", DownloadAsync).AllowAnonymous();
        group.MapDelete("/{mediaId:guid}", DeleteAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> UploadAsync(
        HttpRequest request,
        ClaimsPrincipal principal,
        IMediaService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var ownerUserId))
        {
            return Results.Unauthorized();
        }

        if (!request.HasFormContentType)
        {
            return InvalidForm();
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return InvalidForm();
        }

        await using var content = file.OpenReadStream();
        var result = await service.UploadAsync(
            ownerUserId,
            content,
            file.FileName,
            file.ContentType,
            file.Length,
            form["purpose"].ToString(),
            cancellationToken);

        return result.Succeeded
            ? Results.Created(result.Value!.Url, result.Value)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> DownloadAsync(
        Guid mediaId,
        IMediaService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DownloadAsync(mediaId, cancellationToken);
        return result.Succeeded
            ? Results.Stream(
                result.Value!.Content,
                result.Value.ContentType,
                enableRangeProcessing: false)
            : result.Error!.ToHttpResult();
    }

    private static async Task<IResult> DeleteAsync(
        Guid mediaId,
        ClaimsPrincipal principal,
        IMediaService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorUserId(principal, out var ownerUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.DeleteAsync(ownerUserId, mediaId, cancellationToken);
        return result.Succeeded ? Results.NoContent() : result.Error!.ToHttpResult();
    }

    private static bool TryGetActorUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    private static IResult InvalidForm()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation",
            Detail = "A multipart form containing file and purpose is required."
        };
        problem.Extensions["code"] = "invalid_media_form";
        return Results.Problem(problem);
    }
}
