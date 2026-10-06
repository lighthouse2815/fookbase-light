using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Events.Domain.Enums;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Modules.Events.DTOs.Requests;

public sealed record SetEventRsvpRequest(
    [Required(ErrorMessage = "Trạng thái tham gia là bắt buộc.")]
    [OptionalEnumValue<EventParticipantStatus>(ErrorMessage = "Trạng thái tham gia phải là going hoặc interested.")]
    string Status);
