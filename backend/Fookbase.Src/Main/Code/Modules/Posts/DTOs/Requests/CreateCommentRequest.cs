namespace Fookbase.Api.Modules.Posts.DTOs.Requests;

public sealed record CreateCommentRequest(string Content, Guid? ParentCommentId);
