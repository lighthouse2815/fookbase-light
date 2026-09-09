using Microsoft.AspNetCore.Identity;

namespace Fookbase.Api.Modules.Identity.Entities;

public sealed class User : IdentityUser<Guid>
{
    private User()
    {
    }

    public User(Guid id, string email, string userName, DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        UserName = userName;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsActive { get; private set; }

    public void Disable() => IsActive = false;
}
