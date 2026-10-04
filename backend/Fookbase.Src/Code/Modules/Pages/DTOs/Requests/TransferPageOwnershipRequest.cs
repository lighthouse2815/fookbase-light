using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Pages.DTOs.Requests;

public sealed record TransferPageOwnershipRequest(
    [NonEmptyGuid(ErrorMessage = "Người dùng là bắt buộc.")]
    Guid UserId);
