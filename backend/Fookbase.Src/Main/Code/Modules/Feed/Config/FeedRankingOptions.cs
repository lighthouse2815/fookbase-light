namespace Fookbase.Api.Modules.Feed.Config;

public sealed class FeedRankingOptions
{
    public const string SectionName = "FeedRanking";

    public int OwnAffinity { get; init; } = 8;
    public int FriendAffinity { get; init; } = 6;
    public int GroupAffinity { get; init; } = 4;
    public int PageAffinity { get; init; } = 3;
    public int SuggestedReelAffinity { get; init; } = 1;

    // One affinity point offsets this many hours of age at the session snapshot.
    public int FreshnessHoursPerPoint { get; init; } = 6;
    // Each source must provide at least a full maximum page plus one lookahead row.
    public int CandidateLimitPerSource { get; init; } = 100;
    public int OrganicItemsPerSuggestion { get; init; } = 4;
    public int SuggestedReelMaxAgeDays { get; init; } = 7;

    public void Validate()
    {
        if (new[] { OwnAffinity, FriendAffinity, GroupAffinity, PageAffinity, SuggestedReelAffinity }
                .Any(weight => weight is < 0 or > 100) ||
            FreshnessHoursPerPoint is < 1 or > 168 ||
            CandidateLimitPerSource is < 51 or > 500 ||
            OrganicItemsPerSuggestion is < 2 or > 20 ||
            SuggestedReelMaxAgeDays is < 1 or > 30)
        {
            throw new InvalidOperationException("Feed ranking options are outside the supported bounds.");
        }
    }
}
