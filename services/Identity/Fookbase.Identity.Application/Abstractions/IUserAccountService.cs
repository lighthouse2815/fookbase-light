using Fookbase.Identity.Domain.Entities;

namespace Fookbase.Identity.Application.Abstractions;

public interface IUserAccountService
{
    Task<User?> FindByEmailAsync(string email);

    Task<User?> FindByUserNameAsync(string userName);

    Task<User?> FindByIdAsync(Guid userId);

    Task<UserCreationResult> CreateAsync(User user, string password);

    Task<bool> CheckPasswordAsync(User user, string password);
}

public sealed record UserCreationResult(
    bool Succeeded,
    IReadOnlyDictionary<string, string[]> Errors);
