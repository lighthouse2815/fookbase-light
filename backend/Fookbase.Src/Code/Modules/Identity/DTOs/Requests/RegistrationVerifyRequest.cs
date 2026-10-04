using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Identity.Common;

namespace Fookbase.Api.Modules.Identity.DTOs.Requests;

public sealed record RegistrationVerifyRequest(

    [NonEmptyGuid]
    Guid ChallengeId,

    [Required(ErrorMessage = "Mã OTP là bắt buộc.")]
    [RegularExpression("[0-9]{6}", ErrorMessage = "Mã OTP phải gồm 6 chữ số.")]
    string? Code);
