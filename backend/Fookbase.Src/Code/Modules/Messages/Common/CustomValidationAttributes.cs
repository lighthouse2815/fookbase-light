using System.ComponentModel.DataAnnotations;
using Fookbase.Api.Modules.Messages.Domain.Enums;
using Fookbase.Api.Modules.Messages.DTOs.Requests;

namespace Fookbase.Api.Modules.Messages.Common;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class DistinctMessageIdsAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is null ||
        value is IReadOnlyList<Guid> ids && ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ValidMessageCursorAttribute<TCursor> : ValidationAttribute where TCursor : class
{
    public override bool IsValid(object? value) =>
        value is null ||
        value is string text &&
        (string.IsNullOrWhiteSpace(text) ||
         text.Length <= MessageCursorCodec.MaximumLength && MessageCursorCodec.TryDecode<TCursor>(text));
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ConversationParticipantRoleAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is null ||
        value is string text && Enum.TryParse<ConversationParticipantRole>(text, true, out var role) &&
        role is ConversationParticipantRole.ADMIN or ConversationParticipantRole.MEMBER;
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class MessageContentAttribute : ValidationAttribute
{
    public override bool RequiresValidationContext => true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        value is SendMessageRequest request &&
        string.IsNullOrWhiteSpace(request.Content) && request.MediaIds is not { Count: > 0 }
            ? new ValidationResult(FormatErrorMessage(validationContext.DisplayName),
                [nameof(SendMessageRequest.Content), nameof(SendMessageRequest.MediaIds)])
            : ValidationResult.Success;
}
