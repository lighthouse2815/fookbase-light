using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Messages.Entities;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record EditMessageRequest(
    [Required]
    [TrimmedStringLength(Message.MaximumContentLength, MinimumLength = 1)]
    string Content);
