# Bảng quan hệ entity — Fookbase Light

Nguồn: mô hình runtime EF Core ngày 05/10/2026; không đọc dữ liệu database triển khai. 70 bảng EF + `__EFMigrationsHistory`; 116 FK. Xem [trình xem sơ đồ](index.html), [Word](../Quan-he-entity-Fookbase-Light.docx), [PDF](Quan-he-entity-Fookbase-Light.pdf).

`Cha → con` có dạng `1 → 0..n`, `0..1 → 0..n` hoặc `1 → 0..1`; tham chiếu nghiệp vụ không được coi là FK.

## Identity: tài khoản và phân quyền

[Sơ đồ SVG](01-identity-core.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `AspNetUsers` | `Id` | 17 | 0 |
| `AspNetRoles` | `Id` | 4 | 0 |
| `AspNetUserRoles` | `UserId, RoleId` | 2 | 2 |
| `AspNetUserClaims` | `Id` | 4 | 1 |
| `AspNetRoleClaims` | `Id` | 4 | 1 |
| `AspNetUserLogins` | `LoginProvider, ProviderKey` | 4 | 1 |
| `AspNetUserTokens` | `UserId, LoginProvider, Name` | 4 | 1 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-001 | `AspNetUserRoles(RoleId)` | `AspNetRoles(Id)` | 1 → 0..n | Cascade |
| FK-002 | `AspNetUserRoles(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Cascade |
| FK-003 | `AspNetUserClaims(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Cascade |
| FK-004 | `AspNetRoleClaims(RoleId)` | `AspNetRoles(Id)` | 1 → 0..n | Cascade |
| FK-005 | `AspNetUserLogins(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Cascade |
| FK-006 | `AspNetUserTokens(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Cascade |

Các quy tắc cần chú ý:

- AspNetUserRoles là bảng nối tài khoản và vai trò. AspNetUserLogins có khóa chính ghép LoginProvider + ProviderKey; AspNetUserTokens có UserId + LoginProvider + Name.

## Phiên và xác thực

[Sơ đồ SVG](02-authentication.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `AuthSessions` | `Id` | 7 | 1 |
| `RefreshTokens` | `Id` | 8 | 3 |
| `ExternalLoginTickets` | `Id` | 11 | 1 |
| `PasswordResetOtps` | `Id` | 11 | 1 |
| `RegistrationChallenges` | `Id` | 16 | 0 |
| `TwoFactorLoginChallenges` | `Id` | 7 | 1 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-007 | `AuthSessions(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Cascade |
| FK-008 | `RefreshTokens(ReplacedByTokenId)` | `RefreshTokens(Id)` | 0..1 → 0..n | ClientSetNull |
| FK-009 | `RefreshTokens(SessionId)` | `AuthSessions(Id)` | 1 → 0..n | Cascade |
| FK-010 | `RefreshTokens(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Cascade |
| FK-011 | `ExternalLoginTickets(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Cascade |
| FK-012 | `PasswordResetOtps(UserId)` | `AspNetUsers(Id)` | 1 → 0..1 | Cascade |
| FK-013 | `TwoFactorLoginChallenges(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Cascade |

Các quy tắc cần chú ý:

- PasswordResetOtps.UserId có unique index: mỗi tài khoản có tối đa một dòng OTP trong bảng. RefreshTokens.ReplacedByTokenId là FK tự tham chiếu tùy chọn, không có unique index cho cột này.
- RegistrationChallenges chưa gắn FK với tài khoản; dữ liệu phục vụ quá trình trước khi hoàn thành đăng ký.

## Hồ sơ và quyền riêng tư

[Sơ đồ SVG](03-users.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `UserProfiles` | `UserId` | 18 | 3 |
| `UserPrivacySettings` | `UserId` | 7 | 0 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-014 | `UserProfiles(AvatarMediaId)` | `MediaAssets(Id)` | 0..1 → 0..n | Restrict |
| FK-015 | `UserProfiles(CoverMediaId)` | `MediaAssets(Id)` | 0..1 → 0..n | Restrict |
| FK-016 | `UserProfiles(UserId)` | `AspNetUsers(Id)` | 1 → 0..1 | Cascade |

Các quy tắc cần chú ý:

- UserProfiles.UserId vừa là PK vừa là FK: một tài khoản có tối đa một hồ sơ. UserPrivacySettings.UserId chỉ là PK trong mô hình hiện tại; liên kết tài khoản là tham chiếu nghiệp vụ.

## Bạn bè, theo dõi và chặn

[Sơ đồ SVG](04-friends.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `FriendRequests` | `Id` | 8 | 4 |
| `Friendships` | `Id` | 4 | 2 |
| `UserFollows` | `FollowerUserId, FollowingUserId` | 3 | 2 |
| `BlockedUsers` | `BlockerUserId, BlockedUserId` | 3 | 2 |
| `FriendNotifications` | `Id` | 7 | 3 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-017 | `FriendRequests(ReceiverUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-018 | `FriendRequests(SenderUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-019 | `FriendRequests(UserId1)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-020 | `FriendRequests(UserId2)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-021 | `Friendships(UserId1)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-022 | `Friendships(UserId2)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-023 | `UserFollows(FollowerUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-024 | `UserFollows(FollowingUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-025 | `BlockedUsers(BlockedUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-026 | `BlockedUsers(BlockerUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-027 | `FriendNotifications(ActorUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-028 | `FriendNotifications(FriendRequestId)` | `FriendRequests(Id)` | 1 → 0..n | Restrict |
| FK-029 | `FriendNotifications(RecipientUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |

Các quy tắc cần chú ý:

- FriendRequests có bốn FK tài khoản: SenderUserId, ReceiverUserId, UserId1, UserId2. Cặp chuẩn hóa UserId1/UserId2 có unique index khi Status = 0.
- Friendships không có FK đến FriendRequests. Kết quả chấp nhận lời mời được xử lý ở service. UserFollows và BlockedUsers có khóa chính ghép theo hai vai trò của tài khoản.

## Bài viết, bình luận và cảm xúc

[Sơ đồ SVG](05-posts.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `Posts` | `Id` | 12 | 0 |
| `Comments` | `Id` | 8 | 2 |
| `PostMedia` | `PostId, MediaId` | 3 | 1 |
| `PostReactions` | `PostId, UserId` | 5 | 1 |
| `CommentReactions` | `CommentId, UserId` | 5 | 1 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-030 | `Comments(ParentCommentId)` | `Comments(Id)` | 0..1 → 0..n | Restrict |
| FK-031 | `Comments(PostId)` | `Posts(Id)` | 1 → 0..n | Cascade |
| FK-032 | `PostMedia(PostId)` | `Posts(Id)` | 1 → 0..n | Cascade |
| FK-033 | `PostReactions(PostId)` | `Posts(Id)` | 1 → 0..n | Cascade |
| FK-034 | `CommentReactions(CommentId)` | `Comments(Id)` | 1 → 0..n | Cascade |

Các quy tắc cần chú ý:

- Posts.AuthorUserId là tham chiếu nghiệp vụ. ContainerType/ContainerId xác định nơi chứa bài theo Profile, Group, Page hoặc Event.
- PostMedia.PostId có FK; MediaId trong PostMedia chưa có FK. Comments.ParentCommentId là FK tự tham chiếu tùy chọn.

## Lưu, chia sẻ, đề cập và hashtag

[Sơ đồ SVG](06-social-interactions.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `PostSaves` | `UserId, PostId` | 3 | 1 |
| `PostShares` | `Id` | 8 | 1 |
| `ContentMentions` | `SourceType, SourceId, StartIndex` | 5 | 1 |
| `Hashtags` | `Id` | 4 | 0 |
| `PostHashtags` | `PostId, HashtagId` | 2 | 2 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-035 | `PostSaves(PostId)` | `Posts(Id)` | 1 → 0..n | Cascade |
| FK-036 | `PostShares(OriginalPostId)` | `Posts(Id)` | 1 → 0..n | Cascade |
| FK-037 | `ContentMentions(MentionedUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Cascade |
| FK-038 | `PostHashtags(HashtagId)` | `Hashtags(Id)` | 1 → 0..n | Cascade |
| FK-039 | `PostHashtags(PostId)` | `Posts(Id)` | 1 → 0..n | Cascade |

Các quy tắc cần chú ý:

- ContentMentions.MentionedUserId có FK; SourceType/SourceId xác định Post hoặc Comment. PostShares.OriginalPostId có FK, còn đích chia sẻ dùng DestinationType/DestinationId.

## Media và công việc nền

[Sơ đồ SVG](07-media.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `MediaAssets` | `Id` | 20 | 0 |
| `MediaReferences` | `MediaId, PostId` | 3 | 2 |
| `ProfileMediaReferences` | `UserId, Slot` | 4 | 2 |
| `MediaProcessingJobs` | `Id` | 9 | 1 |
| `ObjectDeletions` | `Id` | 9 | 1 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-040 | `MediaReferences(MediaId)` | `MediaAssets(Id)` | 1 → 0..n | Restrict |
| FK-041 | `MediaReferences(PostId)` | `Posts(Id)` | 1 → 0..n | Cascade |
| FK-042 | `ProfileMediaReferences(MediaId)` | `MediaAssets(Id)` | 1 → 0..n | Restrict |
| FK-043 | `ProfileMediaReferences(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Cascade |
| FK-044 | `MediaProcessingJobs(MediaId)` | `MediaAssets(Id)` | 1 → 0..1 | Cascade |
| FK-045 | `ObjectDeletions(MediaId)` | `MediaAssets(Id)` | 1 → 0..n | Restrict |

Các quy tắc cần chú ý:

- MediaReferences có cả FK MediaId và PostId, khác PostMedia chỉ có FK PostId. MediaProcessingJobs.MediaId có unique index.
- MediaAssets.OwnerUserId là tham chiếu nghiệp vụ. Các bảng tham chiếu media phục vụ quản lý việc sử dụng và vòng đời tệp.

## Nhóm cộng đồng

[Sơ đồ SVG](08-groups.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `Groups` | `Id` | 9 | 2 |
| `GroupMembers` | `GroupId, UserId` | 4 | 2 |
| `GroupJoinRequests` | `Id` | 7 | 3 |
| `GroupInvites` | `Id` | 7 | 3 |
| `GroupRules` | `Id` | 5 | 1 |
| `GroupCoverMediaReferences` | `GroupId` | 3 | 2 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-046 | `Groups(CoverMediaId)` | `MediaAssets(Id)` | 0..1 → 0..n | Restrict |
| FK-047 | `Groups(OwnerUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-048 | `GroupMembers(GroupId)` | `Groups(Id)` | 1 → 0..n | Cascade |
| FK-049 | `GroupMembers(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-050 | `GroupJoinRequests(GroupId)` | `Groups(Id)` | 1 → 0..n | Cascade |
| FK-051 | `GroupJoinRequests(RequesterUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-052 | `GroupJoinRequests(RespondedByUserId)` | `AspNetUsers(Id)` | 0..1 → 0..n | Restrict |
| FK-053 | `GroupInvites(GroupId)` | `Groups(Id)` | 1 → 0..n | Cascade |
| FK-054 | `GroupInvites(InviteeUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-055 | `GroupInvites(InviterUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-056 | `GroupRules(GroupId)` | `Groups(Id)` | 1 → 0..n | Cascade |
| FK-057 | `GroupCoverMediaReferences(GroupId)` | `Groups(Id)` | 1 → 0..1 | Cascade |
| FK-058 | `GroupCoverMediaReferences(MediaId)` | `MediaAssets(Id)` | 1 → 0..n | Restrict |

Các quy tắc cần chú ý:

- Groups.OwnerUserId là FK đến tài khoản chủ nhóm. GroupMembers xác định từng thành viên và Role bằng khóa ghép GroupId/UserId.
- GroupJoinRequests và GroupInvites chỉ cho phép một yêu cầu/lời mời Pending cho một người trong một nhóm qua unique index có điều kiện.
- Groups.CoverMediaId và GroupCoverMediaReferences.MediaId là các FK riêng; database không có ràng buộc trong mô hình buộc hai giá trị bằng nhau.

## Trang cộng đồng

[Sơ đồ SVG](09-pages.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `Pages` | `Id` | 12 | 3 |
| `PageMembers` | `PageId, UserId` | 5 | 2 |
| `PageFollowers` | `PageId, UserId` | 3 | 2 |
| `PageRoleInvitations` | `Id` | 8 | 3 |
| `PageMediaReferences` | `PageId, Slot` | 4 | 2 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-059 | `Pages(AvatarMediaId)` | `MediaAssets(Id)` | 0..1 → 0..n | Restrict |
| FK-060 | `Pages(CoverMediaId)` | `MediaAssets(Id)` | 0..1 → 0..n | Restrict |
| FK-061 | `Pages(CreatedByUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-062 | `PageMembers(PageId)` | `Pages(Id)` | 1 → 0..n | Cascade |
| FK-063 | `PageMembers(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-064 | `PageFollowers(PageId)` | `Pages(Id)` | 1 → 0..n | Cascade |
| FK-065 | `PageFollowers(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-066 | `PageRoleInvitations(InviteeUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-067 | `PageRoleInvitations(InviterUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-068 | `PageRoleInvitations(PageId)` | `Pages(Id)` | 1 → 0..n | Cascade |
| FK-069 | `PageMediaReferences(MediaId)` | `MediaAssets(Id)` | 1 → 0..n | Restrict |
| FK-070 | `PageMediaReferences(PageId)` | `Pages(Id)` | 1 → 0..n | Cascade |

Các quy tắc cần chú ý:

- Pages.CreatedByUserId lưu người tạo. Chủ sở hữu hiện tại được xác định bằng PageMembers.Role = OWNER. Luồng chuyển chủ thay đổi vai trò thành viên.
- PageMediaReferences dùng khóa ghép PageId/Slot để phân biệt avatar và cover. Username của Page có unique index cho những dòng chưa xóa mềm.

## Hội thoại, thành viên và mốc đọc

[Sơ đồ SVG](10-conversations.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `Conversations` | `Id` | 8 | 3 |
| `ConversationParticipants` | `ConversationId, UserId` | 12 | 4 |
| `ConversationReadCursors` | `ConversationId, UserId` | 5 | 3 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-071 | `Conversations(PhotoMediaId)` | `MediaAssets(Id)` | 0..1 → 0..n | Restrict |
| FK-072 | `Conversations(UserId1)` | `AspNetUsers(Id)` | 0..1 → 0..n | Restrict |
| FK-073 | `Conversations(UserId2)` | `AspNetUsers(Id)` | 0..1 → 0..n | Restrict |
| FK-074 | `ConversationParticipants(ConversationId)` | `Conversations(Id)` | 1 → 0..n | Cascade |
| FK-075 | `ConversationParticipants(LastDeliveredMessageId)` | `Messages(Id)` | 0..1 → 0..n | Restrict |
| FK-076 | `ConversationParticipants(LastReadMessageId)` | `Messages(Id)` | 0..1 → 0..n | Restrict |
| FK-077 | `ConversationParticipants(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-078 | `ConversationReadCursors(ConversationId)` | `Conversations(Id)` | 1 → 0..n | Cascade |
| FK-079 | `ConversationReadCursors(LastReadMessageId)` | `Messages(Id)` | 0..1 → 0..n | Restrict |
| FK-080 | `ConversationReadCursors(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |

Các quy tắc cần chú ý:

- Conversations.UserId1/UserId2 là các FK tùy chọn cho hội thoại trực tiếp; ConversationParticipants là bảng thành viên cho hội thoại.
- LastReadMessageId và LastDeliveredMessageId chỉ có FK đến Messages.Id. Quy tắc message phải thuộc cùng hội thoại được service kiểm tra; FK không chứa ConversationId.

## Tin nhắn, đính kèm và thông báo tin

[Sơ đồ SVG](11-messages.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `Messages` | `Id` | 11 | 4 |
| `MessageAttachments` | `MessageId, MediaId` | 3 | 2 |
| `MessageReactions` | `MessageId, UserId` | 5 | 2 |
| `MessageNotifications` | `Id` | 6 | 3 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-081 | `Messages(ConversationId)` | `Conversations(Id)` | 1 → 0..n | Cascade |
| FK-082 | `Messages(ReplyToMessageId)` | `Messages(Id)` | 0..1 → 0..n | Restrict |
| FK-083 | `Messages(SenderUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-084 | `Messages(StoryId)` | `Stories(Id)` | 0..1 → 0..n | Restrict |
| FK-085 | `MessageAttachments(MediaId)` | `MediaAssets(Id)` | 1 → 0..n | Restrict |
| FK-086 | `MessageAttachments(MessageId)` | `Messages(Id)` | 1 → 0..n | Cascade |
| FK-087 | `MessageReactions(MessageId)` | `Messages(Id)` | 1 → 0..n | Cascade |
| FK-088 | `MessageReactions(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-089 | `MessageNotifications(ConversationId)` | `Conversations(Id)` | 1 → 0..n | Cascade |
| FK-090 | `MessageNotifications(MessageId)` | `Messages(Id)` | 1 → 0..n | Cascade |
| FK-091 | `MessageNotifications(RecipientUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |

Các quy tắc cần chú ý:

- Messages.ReplyToMessageId là FK tự tham chiếu tùy chọn; service kiểm tra tin được trả lời thuộc cùng hội thoại. StoryId là FK tùy chọn khi phản hồi Story.
- MessageAttachments có khóa ghép MessageId/MediaId và unique MessageId/SortOrder. MessageNotifications có unique RecipientUserId/MessageId.

## Thông báo và push mobile

[Sơ đồ SVG](12-notifications.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `Notifications` | `Id` | 9 | 2 |
| `PushDevices` | `Id` | 5 | 1 |
| `PushDeliveryReceipts` | `Id` | 6 | 1 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-092 | `Notifications(ActorUserId)` | `AspNetUsers(Id)` | 0..1 → 0..n | Restrict |
| FK-093 | `Notifications(RecipientUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-094 | `PushDevices(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-095 | `PushDeliveryReceipts(PushDeviceId)` | `PushDevices(Id)` | 1 → 0..n | Cascade |

Các quy tắc cần chú ý:

- Notifications.EntityId/EntityType là tham chiếu theo loại, có thể null; xem bảng ánh xạ ở phần cuối. USER_FOLLOW dùng mã tài khoản người theo dõi; EVENT_INVITE dùng mã EventInvitations.
- Notifications không có unique index cho việc chống trùng thông báo. PushDevices có unique ExpoPushToken; PushDeliveryReceipts có unique ExpoReceiptId.

## Báo cáo và kiểm duyệt

[Sơ đồ SVG](13-moderation.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `ContentReports` | `Id` | 9 | 0 |
| `ModerationActions` | `Id` | 11 | 3 |
| `UserModerationStates` | `UserId` | 5 | 1 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-096 | `ModerationActions(ModeratorUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-097 | `ModerationActions(ReportId)` | `ContentReports(Id)` | 0..1 → 0..n | Restrict |
| FK-098 | `ModerationActions(SubjectUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-099 | `UserModerationStates(UserId)` | `AspNetUsers(Id)` | 1 → 0..1 | Cascade |

Các quy tắc cần chú ý:

- ContentReports.ReporterUserId và TargetType/TargetId là tham chiếu nghiệp vụ. Unique ReporterUserId/TargetType/TargetId hạn chế báo cáo trùng theo cùng người và đối tượng.
- ModerationActions có các FK riêng cho tài khoản liên quan, người kiểm duyệt và báo cáo. TargetType/TargetId vẫn là tham chiếu theo loại.

## Sự kiện

[Sơ đồ SVG](14-events.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `Events` | `Id` | 18 | 2 |
| `EventParticipants` | `EventId, UserId` | 4 | 2 |
| `EventInvitations` | `Id` | 7 | 3 |
| `EventCoverMediaReferences` | `EventId` | 3 | 2 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-100 | `Events(CoverMediaId)` | `MediaAssets(Id)` | 0..1 → 0..n | Restrict |
| FK-101 | `Events(CreatedByUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-102 | `EventParticipants(EventId)` | `Events(Id)` | 1 → 0..n | Cascade |
| FK-103 | `EventParticipants(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-104 | `EventInvitations(EventId)` | `Events(Id)` | 1 → 0..n | Cascade |
| FK-105 | `EventInvitations(InviteeUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-106 | `EventInvitations(InviterUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-107 | `EventCoverMediaReferences(EventId)` | `Events(Id)` | 1 → 0..1 | Cascade |
| FK-108 | `EventCoverMediaReferences(MediaId)` | `MediaAssets(Id)` | 1 → 0..n | Restrict |

Các quy tắc cần chú ý:

- Events.HostType/HostId xác định chủ thể User/Group/Page; CreatedByUserId là FK tài khoản tạo.
- EventInvitations không có unique index cho cặp EventId/InviteeUserId Pending trong mô hình này; service kiểm tra trùng lời mời.
- Events.CoverMediaId và EventCoverMediaReferences.MediaId là các FK riêng; tính nhất quán giữa chúng do service quản lý.

## Ảnh và album

[Sơ đồ SVG](15-photos.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `PhotoAlbums` | `Id` | 9 | 0 |
| `AlbumMedia` | `AlbumId, MediaId` | 5 | 0 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| — | Không có FK vật lý | — | — | — |

Các quy tắc cần chú ý:

- PhotoAlbums.OwnerUserId, AlbumMedia.AlbumId và AlbumMedia.MediaId đều là tham chiếu nghiệp vụ, không có FK trong mô hình hiện tại.
- AlbumMedia có PK AlbumId/MediaId để tránh lặp cùng tệp trong cùng album. Unique OwnerUserId/AlbumType chỉ áp dụng khi DeletedAtUtc IS NULL và AlbumType <> 0.

## Stories

[Sơ đồ SVG](16-stories.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `Stories` | `Id` | 8 | 2 |
| `StoryMediaReferences` | `StoryId` | 3 | 2 |
| `StoryViews` | `StoryId, ViewerUserId` | 3 | 2 |
| `StoryReactions` | `StoryId, UserId` | 4 | 2 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| FK-109 | `Stories(AuthorUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-110 | `Stories(MediaId)` | `MediaAssets(Id)` | 1 → 0..n | Restrict |
| FK-111 | `StoryMediaReferences(MediaId)` | `MediaAssets(Id)` | 1 → 0..n | Restrict |
| FK-112 | `StoryMediaReferences(StoryId)` | `Stories(Id)` | 1 → 0..1 | Cascade |
| FK-113 | `StoryViews(StoryId)` | `Stories(Id)` | 1 → 0..n | Cascade |
| FK-114 | `StoryViews(ViewerUserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |
| FK-115 | `StoryReactions(StoryId)` | `Stories(Id)` | 1 → 0..n | Cascade |
| FK-116 | `StoryReactions(UserId)` | `AspNetUsers(Id)` | 1 → 0..n | Restrict |

Các quy tắc cần chú ý:

- StoryViews và StoryReactions dùng khóa chính ghép theo Story và người dùng. StoryMediaReferences.StoryId vừa là PK vừa là FK.
- Stories.MediaId và StoryMediaReferences.MediaId là các FK độc lập; mô hình không có ràng buộc buộc hai cột luôn bằng nhau.

## Lượt xem Reel

[Sơ đồ SVG](17-reels.svg)

| Bảng | PK | Số cột | Số FK |
|---|---|---:|---:|
| `ReelViews` | `Id` | 7 | 0 |

| Mã | Bảng con / FK | Bảng cha / khóa | Cha → con | Xóa cha |
|---|---|---|---|---|
| — | Không có FK vật lý | — | — | — |

Các quy tắc cần chú ý:

- Reel được lưu bằng Posts với PostType = REEL, không có bảng Reels riêng. ReelViews.ReelPostId và ViewerUserId là tham chiếu nghiệp vụ.
- ReelViews dùng PK Id; không có unique index ReelPostId/ViewerUserId, nên không suy ra mỗi người chỉ có một bản ghi lượt xem.

## Tham chiếu nghiệp vụ đơn giản

| Nguồn | Đích | Ý nghĩa |
|---|---|---|
| `UserPrivacySettings.UserId` | `AspNetUsers.Id` | Hồ sơ cài đặt theo người dùng; UserId là PK, không có FK. |
| `MediaAssets.OwnerUserId` | `AspNetUsers.Id` | Tài khoản sở hữu media. |
| `Posts.AuthorUserId` | `AspNetUsers.Id` | Tác giả thực; với Page là dữ liệu audit. |
| `Comments.AuthorUserId` | `AspNetUsers.Id` | Tác giả bình luận. |
| `PostMedia.MediaId` | `MediaAssets.Id` | Tệp đính kèm bài; chỉ phía PostId có FK. |
| `PostReactions.UserId` | `AspNetUsers.Id` | Người thả cảm xúc bài. |
| `CommentReactions.UserId` | `AspNetUsers.Id` | Người thả cảm xúc bình luận. |
| `PostSaves.UserId` | `AspNetUsers.Id` | Người lưu bài. |
| `PostShares.SharingUserId` | `AspNetUsers.Id` | Người chia sẻ. |
| `ContentReports.ReporterUserId` | `AspNetUsers.Id` | Người báo cáo. |
| `PhotoAlbums.OwnerUserId` | `AspNetUsers.Id` | Chủ album. |
| `AlbumMedia.AlbumId` | `PhotoAlbums.Id` | Album chứa ảnh; quan hệ không có FK trong model. |
| `AlbumMedia.MediaId` | `MediaAssets.Id` | Ảnh được album tham chiếu; không có FK trong model. |
| `ReelViews.ReelPostId` | `Posts.Id` | Bài có PostType=REEL; không có entity Reel riêng. |
| `ReelViews.ViewerUserId` | `AspNetUsers.Id` | Người xem Reel. |

## Tham chiếu theo loại

### Posts: ContainerType / ContainerId

Nơi chứa bài; không có FK trực tiếp theo loại.

| Loại | Bảng đích |
|---|---|
| PROFILE | `AspNetUsers` |
| GROUP | `Groups` |
| PAGE | `Pages` |
| EVENT | `Events` |

### PostShares: DestinationType / DestinationId

Đích chia sẻ; khác bài gốc có FK OriginalPostId.

| Loại | Bảng đích |
|---|---|
| PROFILE | `AspNetUsers` |
| GROUP | `Groups` |
| PAGE | `Pages` |

### ContentMentions: SourceType / SourceId

Nội dung chứa đề cập; MentionedUserId có FK riêng.

| Loại | Bảng đích |
|---|---|
| POST | `Posts` |
| COMMENT | `Comments` |

### ContentReports: TargetType / TargetId

Đối tượng bị báo cáo.

| Loại | Bảng đích |
|---|---|
| USER | `AspNetUsers` |
| POST | `Posts` |

### ModerationActions: TargetType / TargetId

Đối tượng quyết định kiểm duyệt; không thay FK SubjectUserId.

| Loại | Bảng đích |
|---|---|
| USER | `AspNetUsers` |
| POST | `Posts` |

### Events: HostType / HostId

Chủ thể tổ chức; CreatedByUserId là tài khoản audit có FK.

| Loại | Bảng đích |
|---|---|
| USER | `AspNetUsers` |
| GROUP | `Groups` |
| PAGE | `Pages` |

### Notifications: EntityType + Type / EntityId

Thực thể điều hướng theo loại; EntityId và EntityType có thể null. USER_FOLLOW dùng mã người theo dõi; EVENT_INVITE dùng mã lời mời sự kiện.

| Loại | Bảng đích |
|---|---|
| FRIEND_REQUEST | `FriendRequests` |
| POST | `Posts` |
| COMMENT | `Comments` |
| GROUP | `Groups` |
| GROUP_JOIN_REQUEST | `GroupJoinRequests` |
| GROUP_INVITE | `GroupInvites` |
| STORY | `Stories` |
| PAGE | `Pages` |
| PAGE_ROLE_INVITATION | `PageRoleInvitations` |
| USER_FOLLOW | `AspNetUsers` |
| EVENT + EVENT_INVITE | `EventInvitations` |
| EVENT (Type khác) | `Events` |

## Unique index

PK cũng đảm bảo tính duy nhất; dưới đây là unique index ngoài PK. NULL giữ ngữ nghĩa unique của PostgreSQL.

| Bảng | Cột | Bộ lọc |
|---|---|---|
| `AspNetRoles` | `NormalizedName` | Toàn bảng |
| `AspNetUsers` | `NormalizedEmail` | Toàn bảng |
| `AspNetUsers` | `NormalizedUserName` | Toàn bảng |
| `AspNetUsers` | `PhoneNumber` | Toàn bảng |
| `ContentReports` | `ReporterUserId, TargetType, TargetId` | Toàn bảng |
| `Conversations` | `UserId1, UserId2` | Toàn bảng |
| `ExternalLoginTickets` | `CodeHash, ExpiresAtUtc` | Toàn bảng |
| `FriendRequests` | `UserId1, UserId2` | "Status" = 0 |
| `Friendships` | `UserId1, UserId2` | Toàn bảng |
| `GroupInvites` | `GroupId, InviteeUserId` | "Status" = 0 |
| `GroupJoinRequests` | `GroupId, RequesterUserId` | "Status" = 0 |
| `Hashtags` | `NormalizedName` | Toàn bảng |
| `MediaAssets` | `ObjectKey` | Toàn bảng |
| `MediaProcessingJobs` | `MediaId` | Toàn bảng |
| `MessageAttachments` | `MessageId, SortOrder` | Toàn bảng |
| `MessageNotifications` | `RecipientUserId, MessageId` | Toàn bảng |
| `PageRoleInvitations` | `PageId, InviteeUserId` | "Status" = 0 |
| `Pages` | `Username` | "DeletedAtUtc" IS NULL |
| `PasswordResetOtps` | `UserId` | Toàn bảng |
| `PhotoAlbums` | `OwnerUserId, AlbumType` | "DeletedAtUtc" IS NULL AND "AlbumType" <> 0 |
| `PostMedia` | `PostId, SortOrder` | Toàn bảng |
| `PushDeliveryReceipts` | `ExpoReceiptId` | Toàn bảng |
| `PushDevices` | `ExpoPushToken` | Toàn bảng |
| `RefreshTokens` | `TokenHash` | Toàn bảng |
| `RegistrationChallenges` | `Contact` | Toàn bảng |
| `UserProfiles` | `Username` | Toàn bảng |

## Bảng hạ tầng EF Core

`__EFMigrationsHistory`: `MigrationId varchar(150)` (PK), `ProductVersion varchar(32)`; không có FK. [Sơ đồ](18-migration-history.svg).
