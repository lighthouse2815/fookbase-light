using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Events.DTOs.Requests;

public sealed record CreateEventInvitationRequest([NonEmptyGuid] Guid UserId);
