using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;
using Microsoft.AspNetCore.Mvc;

namespace Fookbase.Api.Modules.Messages.DTOs.Requests;

public sealed record MessageSearchRequest(
    [FromQuery(Name = "q")]
    [Required]
    [TrimmedStringLength(200, MinimumLength = 1)]
    string Query);
