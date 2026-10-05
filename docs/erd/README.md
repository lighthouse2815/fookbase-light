# Quan hệ entity và sơ đồ ERD Fookbase Light

Bộ tài liệu gồm bảng quan hệ trong [file Word](../Quan-he-entity-Fookbase-Light.docx), [bản PDF](Quan-he-entity-Fookbase-Light.pdf) và sơ đồ SVG có thể phóng to. Sơ đồ được chia thành 17 nhóm để đọc rõ từng chức năng, kèm một sơ đồ riêng cho bảng lịch sử migration.

Nguồn đối chiếu là model EF Core hiện tại của `FookbaseDbContext`, cấu hình/annotation entity và service trong repository, tại ngày 05/10/2026. Model có **70 bảng và 116 FK**; thêm `__EFMigrationsHistory` thành **71 bảng** trong danh mục. Có **15 tham chiếu logic đơn** và **7 nhóm tham chiếu đa hình** được phân biệt với FK. Đây là cấu trúc khai báo trong mã nguồn, không phải bản kiểm kê trực tiếp một database đã triển khai.

## Mở và xem sơ đồ

1. Mở [index.html](index.html) trong Chrome, Firefox hoặc Edge để chọn sơ đồ và xem các tài liệu đi kèm. Có thể mở trực tiếp file trên máy, không cần khởi chạy backend.
2. Chọn một SVG trong danh mục bên dưới để xem riêng. Trong browser, dùng chức năng phóng to để đọc tên cột và đường nối.
3. Trong VS Code, nhấp phải file `.svg`, chọn **Open With… → Image Preview**. Có thể mở từ Explorer nếu editor đang hiển thị mã XML của SVG.
4. Xem [bảng quan hệ dạng Markdown](quan-he.md) để tra các FK và tìm nhanh trong VS Code.
5. Dùng [ef-model.json](ef-model.json) để tra đầy đủ cột, kiểu dữ liệu, nullable, PK, unique index và FK. Sơ đồ chỉ chọn các cột cần thiết để thể hiện quan hệ; các file `.dot` cùng tên giữ nguồn Graphviz của từng sơ đồ.

## Chú giải và cách đọc

| Ký hiệu | Ý nghĩa |
|---|---|
| `PK` | Cột thuộc khóa chính. Nhiều cột có `PK` tạo thành một khóa ghép. |
| `FK` / đường liền | FK thật trong model EF Core, trỏ đến khóa của bảng được tham chiếu. |
| `REF` / đường nét đứt màu nâu | Tham chiếu được xử lý theo nghiệp vụ; model không khai báo FK cho liên kết này. |
| `PK/FK`, `PK/REF` | Cột vừa thuộc PK, vừa là FK hoặc tham chiếu logic. |
| Bảng tiêu đề màu xám | Bảng của nhóm khác, được lặp lại để làm rõ quan hệ; không phải một bảng mới. |
| `?` sau kiểu cột | Cột nullable, có thể không có giá trị. |
| `1` | Mỗi bản ghi ở phía đối diện phải có đúng một bản ghi ở phía này. |
| `0..1` | Có thể không có hoặc có một bản ghi ở phía này. |
| `0..n` | Có thể không có hoặc có nhiều bản ghi ở phía này. |

Đường FK được vẽ từ bảng cha đến bảng chứa FK. Ví dụ `Posts 1 → 0..n Comments` nghĩa là mỗi bình luận thuộc đúng một bài, còn một bài có thể chưa có hoặc có nhiều bình luận. Với FK nullable, số ở đầu bảng cha là `0..1`. FK có tính duy nhất giới hạn phía con ở `0..1`; FK bắt buộc không có nghĩa mỗi bảng cha phải có sẵn một bản ghi con.

Unique index trong JSON có thể áp dụng cho một cột, nhiều cột hoặc chỉ các bản ghi thỏa điều kiện lọc. Ví dụ khóa `(PageId, UserId)` ngăn trùng quan hệ thành viên, nhưng không tự đảm bảo chỉ có một thành viên mang vai trò Owner. Các điều kiện vai trò, trạng thái hoạt động và quyền truy cập còn được kiểm tra trong service. Cardinality của đường `REF` phải đọc cùng quy tắc nghiệp vụ vì database không ràng buộc liên kết đó bằng FK.

## Danh mục 71 bảng

Mỗi bảng được liệt kê đúng một lần trong cột “Bảng thuộc nhóm”. Các bảng màu xám trên SVG chỉ cung cấp ngữ cảnh và không được tính thêm. 17 nhóm dưới đây là cách chia tài liệu để xem sơ đồ; một số mô-đun source như Identity, Posts hoặc Messages được tách thành nhiều nhóm.

| STT | Nhóm / sơ đồ SVG | Số bảng | Bảng thuộc nhóm |
|---|---|---:|---|
| 01 | [Identity: tài khoản và phân quyền](01-identity-core.svg) | 7 | `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens` |
| 02 | [Phiên và xác thực](02-authentication.svg) | 6 | `AuthSessions`, `RefreshTokens`, `ExternalLoginTickets`, `PasswordResetOtps`, `RegistrationChallenges`, `TwoFactorLoginChallenges` |
| 03 | [Hồ sơ và quyền riêng tư](03-users.svg) | 2 | `UserProfiles`, `UserPrivacySettings` |
| 04 | [Bạn bè, theo dõi và chặn](04-friends.svg) | 5 | `FriendRequests`, `Friendships`, `UserFollows`, `BlockedUsers`, `FriendNotifications` |
| 05 | [Bài viết, bình luận và cảm xúc](05-posts.svg) | 5 | `Posts`, `Comments`, `PostMedia`, `PostReactions`, `CommentReactions` |
| 06 | [Lưu, chia sẻ, đề cập và hashtag](06-social-interactions.svg) | 5 | `PostSaves`, `PostShares`, `ContentMentions`, `Hashtags`, `PostHashtags` |
| 07 | [Media và công việc nền](07-media.svg) | 5 | `MediaAssets`, `MediaReferences`, `ProfileMediaReferences`, `MediaProcessingJobs`, `ObjectDeletions` |
| 08 | [Nhóm cộng đồng](08-groups.svg) | 6 | `Groups`, `GroupMembers`, `GroupJoinRequests`, `GroupInvites`, `GroupRules`, `GroupCoverMediaReferences` |
| 09 | [Trang cộng đồng](09-pages.svg) | 5 | `Pages`, `PageMembers`, `PageFollowers`, `PageRoleInvitations`, `PageMediaReferences` |
| 10 | [Hội thoại, thành viên và mốc đọc](10-conversations.svg) | 3 | `Conversations`, `ConversationParticipants`, `ConversationReadCursors` |
| 11 | [Tin nhắn, đính kèm và thông báo tin](11-messages.svg) | 4 | `Messages`, `MessageAttachments`, `MessageReactions`, `MessageNotifications` |
| 12 | [Thông báo và push mobile](12-notifications.svg) | 3 | `Notifications`, `PushDevices`, `PushDeliveryReceipts` |
| 13 | [Báo cáo và kiểm duyệt](13-moderation.svg) | 3 | `ContentReports`, `ModerationActions`, `UserModerationStates` |
| 14 | [Sự kiện](14-events.svg) | 4 | `Events`, `EventParticipants`, `EventInvitations`, `EventCoverMediaReferences` |
| 15 | [Ảnh và album](15-photos.svg) | 2 | `PhotoAlbums`, `AlbumMedia` |
| 16 | [Stories](16-stories.svg) | 4 | `Stories`, `StoryMediaReferences`, `StoryViews`, `StoryReactions` |
| 17 | [Lượt xem Reel](17-reels.svg) | 1 | `ReelViews` |
| 18 | [Lịch sử migration](18-migration-history.svg) | 1 | `__EFMigrationsHistory` |
| | **Tổng** | **71** | **70 bảng từ model EF Core + 1 bảng hệ thống migration** |

## Những quan hệ cần đọc đúng

- **Page creator khác Owner hiện tại.** `Pages.CreatedByUserId` có FK tới người tạo. Owner được xác định từ `PageMembers.Role`; chuyển Owner đổi vai trò hai thành viên, không thay `CreatedByUserId`. `Groups` có `OwnerUserId` riêng, nên cách mô hình hóa hai loại cộng đồng khác nhau.
- **Không phải mọi cột `…Id` đều là FK.** `UserPrivacySettings.UserId` là PK và tham chiếu logic tới tài khoản. `PhotoAlbums.OwnerUserId`, hai phía `AlbumMedia`, `MediaAssets.OwnerUserId` và các ID người dùng trong bài/cảm xúc/lưu/chia sẻ có những liên kết không được model khai báo FK. Xem `REF` thay vì suy ra ràng buộc từ tên cột.
- **PostMedia khác MediaReferences.** `PostMedia` giữ thứ tự đính kèm và chỉ có FK phía `PostId`; `MediaReferences` bảo vệ việc sử dụng tệp và có FK thật cả `MediaId` lẫn `PostId`. Model không dùng một `MediaReference.TargetId` đa hình cho mọi tài nguyên.
- **Reel dùng bảng Posts.** `ReelViews.ReelPostId` tham chiếu logic tới bài có `PostType = REEL`; không có bảng entity `Reels` riêng trong model. Bài Reel còn dùng media video và quyền truy cập tương ứng.
- **FK không thay kiểm tra cùng hội thoại hoặc ACL.** `ReplyToMessageId`, `LastReadMessageId` và `LastDeliveredMessageId` trỏ tới `Messages`. FK đảm bảo tin tồn tại, nhưng không tự đảm bảo tin thuộc đúng hội thoại, người thao tác còn là thành viên hoặc còn quyền xem. Luồng gửi trả lời và đánh dấu đọc trong service kiểm tra điều kiện tương ứng. Tương tự, `Comments.ParentCommentId` không tự giới hạn phản hồi một tầng hoặc cùng bài.
- **Bảng nối và unique có phạm vi cụ thể.** `GroupMembers`, `PageMembers`, `PageFollowers`, `PostHashtags`, `MediaReferences` có FK cả hai phía. `PostReactions`, `CommentReactions`, `PostSaves` vẫn biểu diễn quan hệ nhiều–nhiều về nghiệp vụ nhưng phía người dùng là `REF`. `(PageId, Slot)` và `(UserId, Slot)` chỉ đảm bảo một tham chiếu cho mỗi vị trí ảnh.

## Tham chiếu đa hình

Các nhóm sau được trình bày riêng trong Word/PDF và gắn `REF` trên sơ đồ; không vẽ như FK trực tiếp tới tất cả đích:

| Bảng và trường phân loại / ID | Đích theo nghiệp vụ |
|---|---|
| `Posts.ContainerType + ContainerId` | Hồ sơ người dùng, Group, Page hoặc Event. |
| `PostShares.DestinationType + DestinationId` | Hồ sơ người chia sẻ, Group hoặc Page; bài gốc dùng FK riêng `OriginalPostId`. |
| `ContentMentions.SourceType + SourceId` | Post hoặc Comment; người được đề cập có FK riêng `MentionedUserId`. |
| `ContentReports.TargetType + TargetId` | User hoặc Post. |
| `ModerationActions.TargetType + TargetId` | User hoặc Post; khác FK tới tài khoản chịu quyết định. |
| `Events.HostType + HostId` | User, Group hoặc Page; `CreatedByUserId` là tài khoản thao tác có FK riêng. |
| `Notifications.EntityType + Type + EntityId` | Thực thể điều hướng tùy loại thông báo; trường liên quan có thể null. `EVENT_INVITE` dùng ID lời mời sự kiện, còn các loại sự kiện khác dùng ID Event. |

## Nguồn đối chiếu

- [FookbaseDbContext](../../backend/Fookbase.Src/Code/Persistence/FookbaseDbContext.cs) và [model snapshot](../../backend/Fookbase.Src/Code/Persistence/Migrations/FookbaseDbContextModelSnapshot.cs): cấu hình model, khóa và quan hệ.
- [PagesService](../../backend/Fookbase.Src/Code/Modules/Pages/Services/PagesService.cs): chuyển vai trò Owner.
- [MessagesService](../../backend/Fookbase.Src/Code/Modules/Messages/Services/MessagesService.cs): kiểm tra tin trả lời, mốc đọc và quyền hội thoại.
- [PostsService](../../backend/Fookbase.Src/Code/Modules/Posts/Services/PostsService.cs): bình luận cha và quyền tương tác.
- [ReelMediaQuery](../../backend/Fookbase.Src/Code/Modules/Reels/Services/ReelMediaQuery.cs): bài Reel và video sẵn sàng.

Khi schema thay đổi, cập nhật dữ liệu model, bảng quan hệ và các SVG liên quan cùng nhau. Ràng buộc xóa trong FK áp dụng khi xóa vật lý; `DeletedAtUtc`, trạng thái nghiệp vụ và việc dọn file còn được xử lý bởi các luồng service/worker.
