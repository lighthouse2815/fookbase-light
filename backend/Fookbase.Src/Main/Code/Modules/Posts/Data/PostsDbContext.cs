using Fookbase.Api.Modules.Posts.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Posts.Data;

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
    public DbSet<KnownMedia> KnownMedia => Set<KnownMedia>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PostsDbContext).Assembly,
            type => type.Namespace?.StartsWith(
                "Fookbase.Api.Modules.Posts.Data.Configurations",
                StringComparison.Ordinal) == true);
    }
}
