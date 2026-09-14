namespace Fookbase.Api.Modules.Feed.Config;

public sealed class FeedRankingOptions
{
    public const string SectionName = "FeedRanking";

    public int OwnAffinity { get; init; } = 8;
    public int FriendAffinity { get; init; } = 6;
    public int FollowedNonFriendProfile { get; init; } = 5;
    public int GroupAffinity { get; init; } = 4;
    public int PageAffinity { get; init; } = 3;
    public int SuggestedReelAffinity { get; init; } = 1;

    // One affinity point offsets this many hours of age at the session snapshot.
    public int FreshnessHoursPerPoint { get; init; } = 6;
    public int InteractionLookbackDays { get; init; } = 60;
    public int ReactionAffinityWeight { get; init; } = 1;
    public int CommentAffinityWeight { get; init; } = 2;
    public int ShareAffinityWeight { get; init; } = 2;
    public int SaveAffinityWeight { get; init; } = 2;
    public int ReelCompletionWeight { get; init; } = 3;
    public int ReelStrongCompletionThreshold { get; init; } = 75;
    public int ReelEarlyExitPenalty { get; init; } = 1;
    public int CreatorAffinityCap { get; init; } = 16;
    public int SourceAffinityCap { get; init; } = 12;
    public int ReelWatchAffinityCap { get; init; } = 12;
    public int ReactionEngagementWeight { get; init; } = 1;
    public int CommentEngagementWeight { get; init; } = 1;
    public int ShareEngagementWeight { get; init; } = 1;
    public int EngagementNormalizationCap { get; init; } = 12;
    public int EngagementMinutesPerPoint { get; init; } = 15;
    // Each source must provide at least a full maximum page plus one lookahead row.
    public int CandidateLimitPerSource { get; init; } = 100;
    public int OrganicItemsPerSuggestion { get; init; } = 4;
    public int SuggestedReelMaxAgeDays { get; init; } = 7;

    public void Validate()
    {
        if (new[] { OwnAffinity, FriendAffinity, FollowedNonFriendProfile, GroupAffinity, PageAffinity, SuggestedReelAffinity }
                .Any(weight => weight is < 0 or > 100) ||
            FreshnessHoursPerPoint is < 1 or > 168 ||
            InteractionLookbackDays is < 7 or > 180 ||
            new[] { ReactionAffinityWeight, CommentAffinityWeight, ShareAffinityWeight, SaveAffinityWeight,
                ReelCompletionWeight, ReelEarlyExitPenalty, ReactionEngagementWeight, CommentEngagementWeight,
                ShareEngagementWeight }.Any(weight => weight is < 0 or > 20) ||
            ReelStrongCompletionThreshold is < 50 or > 100 ||
            CreatorAffinityCap is < 1 or > 100 ||
            SourceAffinityCap is < 1 or > 100 ||
            ReelWatchAffinityCap is < 1 or > 100 ||
            EngagementNormalizationCap is < 1 or > 100 ||
            EngagementMinutesPerPoint is < 1 or > 60 ||
            CandidateLimitPerSource is < 51 or > 500 ||
            OrganicItemsPerSuggestion is < 2 or > 20 ||
            SuggestedReelMaxAgeDays is < 1 or > 30)
        {
            throw new InvalidOperationException("Feed ranking options are outside the supported bounds.");
        }
    }
}
