using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record RegistrationResendRequest(
    [NonEmptyGuid]
    Guid ChallengeId);
