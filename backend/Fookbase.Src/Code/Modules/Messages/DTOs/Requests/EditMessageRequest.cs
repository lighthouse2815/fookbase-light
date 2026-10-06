using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record EditMessageRequest(
    [Required]
    [TrimmedStringLength(5_000, MinimumLength = 1)]
    string Content);
