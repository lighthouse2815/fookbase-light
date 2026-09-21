namespace Fookbase.Api.Modules.Friends.Config;

public sealed class FriendSuggestionOptions
{
    public const string SectionName = "FriendSuggestions";

    public int DefaultPageSize { get; init; } = 20;
    public int MaximumPageSize { get; init; } = 50;
    public int CandidateLimitPerSource { get; init; } = 250;
    public int MutualFriendWeight { get; init; } = 3;
    public int SharedGroupWeight { get; init; } = 2;
    public int SharedPageWeight { get; init; } = 1;

    public void Validate()
    {
        if (DefaultPageSize is < 1 or > 50 ||
            MaximumPageSize is < 1 or > 50 ||
            DefaultPageSize > MaximumPageSize ||
            CandidateLimitPerSource is < 51 or > 1_000 ||
            new[] { MutualFriendWeight, SharedGroupWeight, SharedPageWeight }
                .Any(weight => weight is < 0 or > 100))
        {
            throw new InvalidOperationException("Friend suggestion options are outside the supported bounds.");
        }
    }
}
