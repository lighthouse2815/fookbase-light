namespace Fookbase.Api.Modules.Pages.DTOs.Requests;

public sealed record SetPageMediaRequest(Guid? AvatarMediaId, Guid? CoverMediaId, bool RemoveAvatar = false, bool RemoveCover = false);
