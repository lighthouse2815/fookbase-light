using Fookbase.Api.Modules.Posts.Domain.Enums;
using Fookbase.Api.Modules.Stories.Entities;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class StoryNormalizationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void Blank_caption_is_stored_as_null(string? caption)
    {
        Assert.Null(CreateStory(caption).Caption);
    }

    [Fact]
    public void Caption_is_trimmed_and_preserves_internal_line_breaks()
    {
        Assert.Equal("First line\nSecond line", CreateStory(" First line\nSecond line ").Caption);
    }

    [Fact]
    public void Caption_length_is_checked_after_trimming()
    {
        var caption = new string('a', 2_200);
        Assert.Equal(caption, CreateStory($" {caption} ").Caption);
        var error = Assert.Throws<ArgumentException>(() => CreateStory(caption + "a"));
        Assert.Equal("Story caption cannot exceed 2200 characters.", error.Message);
    }

    private static Story CreateStory(string? caption) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), caption, PostPrivacy.PUBLIC,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(24));
}
