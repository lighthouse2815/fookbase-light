using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Posts.DTOs.Requests;

public sealed record SetReactionRequest(
    [Required(ErrorMessage = "Loại cảm xúc là bắt buộc.")]
    [OptionalEnumValue<ReactionType>(ErrorMessage = "Loại cảm xúc phải là like, love, haha, wow, sad hoặc angry.")]
    string Type);
