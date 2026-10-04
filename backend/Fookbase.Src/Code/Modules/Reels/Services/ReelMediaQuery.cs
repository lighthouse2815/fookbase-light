using Fookbase.Api.Modules.Media.Domain.Enums;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Posts.Entities;

namespace Fookbase.Api.Modules.Reels.Services;

public static class ReelMediaQuery
{
    public static IQueryable<MediaAsset> ReadyVideos(IQueryable<MediaAsset> assets) =>
        assets.Where(asset =>
            asset.MediaType == MediaType.VIDEO && asset.Status == MediaStatus.READY &&
            asset.DeletedAtUtc == null && asset.ProcessedObjectKey != null &&
            asset.PosterObjectKey != null && asset.DurationMs != null &&
            asset.Width != null && asset.Height != null);

    public static IQueryable<Post> ApplyReadyMedia(IQueryable<Post> posts, FookbaseDbContext dbContext)
    {
        var readyVideos = ReadyVideos(dbContext.MediaAssets);
        return posts.Where(post => dbContext.PostMedia.Any(postMedia =>
            postMedia.PostId == post.Id && readyVideos.Any(asset => asset.Id == postMedia.MediaId)));
    }
}
