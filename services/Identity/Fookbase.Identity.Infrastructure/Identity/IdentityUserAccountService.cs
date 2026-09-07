using Fookbase.Identity.Application.Abstractions;
using Fookbase.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Fookbase.Identity.Infrastructure.Identity;

internal sealed class IdentityUserAccountService(UserManager<User> userManager)
    : IUserAccountService
{
    public Task<User?> FindByEmailAsync(string email) =>
        userManager.FindByEmailAsync(email);

    public Task<User?> FindByUserNameAsync(string userName) =>
        userManager.FindByNameAsync(userName);

    public Task<User?> FindByIdAsync(Guid userId) =>
        userManager.FindByIdAsync(userId.ToString());

    public async Task<UserCreationResult> CreateAsync(User user, string password)
    {
        var result = await userManager.CreateAsync(user, password);
        var errors = result.Errors
            .GroupBy(error => error.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray(),
                StringComparer.Ordinal);

        return new UserCreationResult(result.Succeeded, errors);
    }

    public Task<bool> CheckPasswordAsync(User user, string password) =>
        userManager.CheckPasswordAsync(user, password);
}
