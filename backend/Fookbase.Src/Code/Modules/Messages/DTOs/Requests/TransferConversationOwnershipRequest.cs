using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record TransferConversationOwnershipRequest([NonEmptyGuid] Guid UserId);
