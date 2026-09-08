using Fookbase.Api.Modules.Identity.Services.Abstractions;
using Fookbase.Api.Modules.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace Fookbase.Api.Modules.Identity.Services.Identity;

internal sealed class IdentityUserAccountService(UserManager<User> userManager)
    : IUserAccountService
{
    public Task<User?> FindByEmailAsync(string email) =>
        userManager.FindByEmailAsync(email);

    public Task<User?> FindByUserNameAsync(string userName) =>
        userManager.FindByNameAsync(userName);

    public Task<User?> FindByIdAsync(Guid userId) =>
        userManager.FindByIdAsync(userId.ToString());

    public Task<bool> CheckPasswordAsync(User user, string password) =>
        userManager.CheckPasswordAsync(user, password);
}
