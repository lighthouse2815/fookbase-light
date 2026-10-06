using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Messages.Entities;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record UpdateConversationRequest(
    [TrimmedStringLength(120, MinimumLength = 1)]
    string? Title = null,

    Guid? PhotoMediaId = null,
    bool RemovePhoto = false,
    DateTimeOffset? MutedUntilUtc = null,
    bool? Archived = null,

    [TrimmedStringLength(ConversationParticipant.MaximumNicknameLength)]
    string? Nickname = null);
