namespace Fookbase.Api.Modules.Pages.DTOs.Requests;

public sealed record CreatePageRequest(string Name, string Username, string Category, string? Bio);
public sealed record UpdatePageRequest(string Name, string Username, string Category, string? Bio);
public sealed record SetPageMediaRequest(Guid? AvatarMediaId, Guid? CoverMediaId, bool RemoveAvatar = false, bool RemoveCover = false);
public sealed record CreatePageRoleInvitationRequest(Guid UserId, string Role);
public sealed record ChangePageMemberRoleRequest(string Role);
public sealed record TransferPageOwnershipRequest(Guid UserId);
