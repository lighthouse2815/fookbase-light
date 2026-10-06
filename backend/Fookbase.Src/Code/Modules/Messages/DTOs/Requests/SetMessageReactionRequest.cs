using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Messages.Domain.Enums;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record SetMessageReactionRequest(
    [Required]
    [OptionalEnumValue<MessageReactionType>]
    string Type);
