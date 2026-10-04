using Fookbase.Api.Modules.Groups.Entities;
using Fookbase.Api.Modules.Identity.Common;
using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Groups.DTOs.Requests;

public sealed record UpdateGroupRuleRequest(
    [Required(ErrorMessage = "Tiêu đề nội quy là bắt buộc.")]
    [TrimmedStringLength(GroupRule.MaximumTitleLength)]
    string Title,

    [TrimmedStringLength(GroupRule.MaximumDescriptionLength)]
    string? Description,

    int SortOrder);
