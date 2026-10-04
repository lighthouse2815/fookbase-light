using Fookbase.Api.Modules.Memories.DTOs.Responses;
using Fookbase.Api.Modules.Posts.Entities;
using Fookbase.Api.Modules.Posts.Services;
using Microsoft.EntityFrameworkCore;

namespace Fookbase.Api.Modules.Memories.Services;

public sealed class MemoriesService(FookbaseDbContext dbContext, PostsService postsService, TimeProvider timeProvider)
{
    private const int MaximumItems = 100;

    public async Task<MemoryTodayResponse> GetTodayAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var posts = await dbContext.Posts.AsNoTracking()
            .Where(post => post.AuthorUserId == ownerUserId && post.ContainerType == PostContainerType.PROFILE &&
                post.PostType == PostType.STANDARD && post.DeletedAtUtc == null && post.CreatedAtUtc.Year < today.Year &&
                post.CreatedAtUtc.Month == today.Month && post.CreatedAtUtc.Day == today.Day)
            .OrderByDescending(post => post.CreatedAtUtc).Take(MaximumItems).ToListAsync(cancellationToken);
        var items = await postsService.LoadResponsesAsync(posts, ownerUserId, cancellationToken);
        var years = posts.Zip(items).GroupBy(item => item.First.CreatedAtUtc.Year).OrderByDescending(group => group.Key)
            .Select(group => new MemoryYearResponse(group.Key, today.Year - group.Key, group.Select(item => item.Second).ToList())).ToList();
        return new MemoryTodayResponse($"{today.Month:D2}-{today.Day:D2}", years);
    }
}
