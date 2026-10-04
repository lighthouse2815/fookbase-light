using Fookbase.Api.Modules.Media.Domain.Enums;
using Fookbase.Api.Modules.Notifications.Domain.Enums;
using Fookbase.Api.Modules.Media.Entities;
using Fookbase.Api.Modules.Notifications.Entities;
using Fookbase.Api.Modules.Users.Domain.Enums;
using Fookbase.Api.Shared.Common;

namespace Fookbase.Api.IntegrationTests;

public sealed class EnumTextTests
{
    [Fact]
    public void Api_names_preserve_existing_spelling()
    {
        Assert.Equal("OnlyMe", BirthdayVisibility.ONLY_ME.ToApiName());
        Assert.Equal("FriendsOfFriends", FriendRequestPolicy.FRIENDS_OF_FRIENDS.ToApiName());
        Assert.Equal("PendingUpload", MediaStatus.PENDING_UPLOAD.ToApiName());
        Assert.Equal("FriendRequestReceived", NotificationType.FRIEND_REQUEST_RECEIVED.ToApiName());
        Assert.Equal("Public", BirthdayVisibility.PUBLIC.ToApiName());
        Assert.Equal("42", ((BirthdayVisibility)42).ToApiName());
    }

    [Theory]
    [InlineData("FriendsOfFriends")]
    [InlineData("friendsOfFriends")]
    [InlineData("friendsoffriends")]
    [InlineData("FRIENDS_OF_FRIENDS")]
    [InlineData("friends_of_friends")]
    [InlineData("  FriendsOfFriends  ")]
    [InlineData("Everyone, FriendsOfFriends")]
    [InlineData("1")]
    public void Parsing_accepts_existing_names_and_uppercase_names(string text)
    {
        Assert.True(EnumText.TryParse(text, true, out FriendRequestPolicy value));
        Assert.Equal(FriendRequestPolicy.FRIENDS_OF_FRIENDS, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("friends__of__friends")]
    public void Parsing_rejects_invalid_names(string? text)
    {
        Assert.False(EnumText.TryParse(text, true, out FriendRequestPolicy _));
    }

    [Fact]
    public void Parsing_keeps_undefined_numbers_for_existing_validation()
    {
        Assert.True(EnumText.TryParse("42", true, out FriendRequestPolicy value));
        Assert.False(Enum.IsDefined(value));
    }
}
