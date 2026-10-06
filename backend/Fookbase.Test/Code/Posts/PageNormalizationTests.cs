using Fookbase.Api.Modules.Pages.Common;
using Fookbase.Api.Modules.Pages.Entities;

namespace Fookbase.Posts.Api.IntegrationTests;

public sealed class PageNormalizationTests
{
    [Theory]
    [InlineData("  My.PAGE_123  ", "my.page_123")]
    [InlineData("abc", "abc")]
    public void Username_is_trimmed_and_lowercased(string value, string expected)
    {
        Assert.Equal(expected, PageNormalization.NormalizeUsername(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("name with spaces")]
    [InlineData("name-with-dash")]
    [InlineData("name/with/slash")]
    [InlineData("name@domain")]
    public void Invalid_usernames_are_rejected(string? value)
    {
        Assert.Throws<ArgumentException>(() => PageNormalization.NormalizeUsername(value));
    }

    [Fact]
    public void Username_length_is_checked_after_trimming()
    {
        var username = new string('a', 50);
        Assert.Equal(username, PageNormalization.NormalizeUsername($" {username} "));
        Assert.Throws<ArgumentException>(() => PageNormalization.NormalizeUsername(new string('a', 51)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_and_category_require_nonblank_text(string? value)
    {
        Assert.Throws<ArgumentException>(() => PageNormalization.NormalizeName(value));
        Assert.Throws<ArgumentException>(() => PageNormalization.NormalizeCategory(value));
    }

    [Fact]
    public void Name_length_is_checked_after_trimming()
    {
        var name = new string('a', 120);
        Assert.Equal(name, PageNormalization.NormalizeName($" {name} "));
        Assert.Equal("A", PageNormalization.NormalizeName(" A "));
        Assert.Throws<ArgumentException>(() => PageNormalization.NormalizeName(new string('a', 121)));
    }

    [Fact]
    public void Category_length_is_checked_after_trimming()
    {
        var category = new string('a', 80);
        Assert.Equal(category, PageNormalization.NormalizeCategory($" {category} "));
        Assert.Equal("A", PageNormalization.NormalizeCategory(" A "));
        Assert.Throws<ArgumentException>(() => PageNormalization.NormalizeCategory(new string('a', 81)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void Blank_bio_is_stored_as_null(string? value)
    {
        Assert.Null(PageNormalization.NormalizeBio(value));
    }

    [Fact]
    public void Bio_is_trimmed_and_preserves_internal_line_breaks()
    {
        Assert.Equal("First line\nSecond line", PageNormalization.NormalizeBio(" First line\nSecond line "));
    }

    [Fact]
    public void Bio_length_is_checked_after_trimming()
    {
        var bio = new string('a', 2_000);
        Assert.Equal(bio, PageNormalization.NormalizeBio($" {bio} "));
        Assert.Throws<ArgumentException>(() => PageNormalization.NormalizeBio(new string('a', 2_001)));
    }

    [Fact]
    public void Page_creation_and_update_normalize_all_text_fields()
    {
        var page = new Page(Guid.NewGuid(), " First Page ", " First.PAGE ", " Community ", " First bio ",
            Guid.NewGuid(), DateTimeOffset.UnixEpoch);
        Assert.Equal("First Page", page.Name);
        Assert.Equal("first.page", page.Username);
        Assert.Equal("Community", page.Category);
        Assert.Equal("First bio", page.Bio);

        page.Update(" Updated Page ", " Updated.PAGE ", " Business ", " \t ", DateTimeOffset.UnixEpoch.AddMinutes(1));
        Assert.Equal("Updated Page", page.Name);
        Assert.Equal("updated.page", page.Username);
        Assert.Equal("Business", page.Category);
        Assert.Null(page.Bio);
    }
}
