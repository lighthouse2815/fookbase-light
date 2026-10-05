using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Events.Domain.Enums;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Events.DTOs.Requests;

public sealed record SetEventRsvpRequest(
    [Required(ErrorMessage = "Trạng thái tham gia là bắt buộc.")]
    [CustomValidation(typeof(SetEventRsvpRequest), nameof(SetEventRsvpRequest.ValidateStatus))]
    string Status)
{
    public static ValidationResult? ValidateStatus(string? value) =>
        EnumText.TryParse(value, true, out EventParticipantStatus parsed) && Enum.IsDefined(parsed)
            ? ValidationResult.Success
            : new ValidationResult("Trạng thái tham gia phải là going hoặc interested.");
}
