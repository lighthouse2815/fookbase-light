using Fookbase.Posts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Posts.Infrastructure.Persistence;

public sealed class PostsDbContext(DbContextOptions<PostsDbContext> options) : DbContext(options)
{
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<PostReaction> PostReactions => Set<PostReaction>();
    public DbSet<KnownUser> KnownUsers => Set<KnownUser>();
    public DbSet<FriendEdge> FriendEdges => Set<FriendEdge>();
    public DbSet<BlockedEdge> BlockedEdges => Set<BlockedEdge>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PostsDbContext).Assembly);
    }
}
