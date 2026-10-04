using System.ComponentModel.DataAnnotations;

namespace Fookbase.Api.Modules.Messages.Common;

public static class MessageRequestValidation
{
    public static ValidationResult? ValidateDistinctIds(IReadOnlyList<Guid>? values) =>
        values is null || values.All(id => id != Guid.Empty) && values.Distinct().Count() == values.Count
            ? ValidationResult.Success
            : new ValidationResult("Danh sách ID phải khác rỗng và không được trùng nhau.");
}
