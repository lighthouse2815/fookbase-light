using Fookbase.Friends.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Friends.Infrastructure.Persistence;

public sealed class FriendsDbContext(DbContextOptions<FriendsDbContext> options)
    : DbContext(options)
{
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();

    public DbSet<Friendship> Friendships => Set<Friendship>();

    public DbSet<BlockedUser> BlockedUsers => Set<BlockedUser>();

    public DbSet<KnownUser> KnownUsers => Set<KnownUser>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FriendsDbContext).Assembly);
    }
}
