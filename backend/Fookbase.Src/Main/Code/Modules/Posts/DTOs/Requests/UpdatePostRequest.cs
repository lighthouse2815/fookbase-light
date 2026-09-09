namespace Fookbase.Api.Modules.Posts.DTOs.Requests;

public sealed record UpdatePostRequest(string Content, string Privacy, IReadOnlyList<Guid>? MediaIds = null);
