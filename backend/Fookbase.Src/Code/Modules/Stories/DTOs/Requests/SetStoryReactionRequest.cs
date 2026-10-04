using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Stories.DTOs.Requests;

public sealed record SetStoryReactionRequest(
    [Required(ErrorMessage = "Loại cảm xúc là bắt buộc.")]
    string? Type);
