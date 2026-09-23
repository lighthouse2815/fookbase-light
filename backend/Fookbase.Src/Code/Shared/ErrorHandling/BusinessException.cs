using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.Shared.ErrorHandling;

public sealed class BusinessException(ApplicationError error) : Exception(error.Message)
{
    public ApplicationError Error { get; } = error;
}
