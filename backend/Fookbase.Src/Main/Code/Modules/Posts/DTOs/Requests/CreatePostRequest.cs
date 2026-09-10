namespace Fookbase.Api.Modules.Posts.DTOs.Requests;

public sealed record CreatePostRequest(string Content, string Privacy, IReadOnlyList<Guid>? MediaIds = null);
