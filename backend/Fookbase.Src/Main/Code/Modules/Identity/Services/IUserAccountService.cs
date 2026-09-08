using Fookbase.Api.Modules.Identity.Entities;

namespace Fookbase.Api.Modules.Identity.Services;

public interface IUserAccountService
{
    Task<User?> FindByEmailAsync(string email);

    Task<User?> FindByUserNameAsync(string userName);

    Task<User?> FindByIdAsync(Guid userId);

    Task<bool> CheckPasswordAsync(User user, string password);
}

public sealed record UserCreationResult(
    bool Succeeded,
    IReadOnlyDictionary<string, string[]> Errors);
