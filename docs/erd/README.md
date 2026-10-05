# Quan hệ entity và sơ đồ ERD Fookbase Light

Bộ tài liệu gồm bảng quan hệ trong [file Word](../Quan-he-entity-Fookbase-Light.docx), [bản PDF](Quan-he-entity-Fookbase-Light.pdf) và sơ đồ SVG có thể phóng to. Trang xem có một sơ đồ tổng quan, 14 mục module và một mục hạ tầng. Word/PDF giữ phần đặc tả chi tiết theo các nhóm chức năng.

Nguồn đối chiếu là model EF Core hiện tại của `FookbaseDbContext`, cấu hình/annotation entity và service trong repository, tại ngày 05/10/2026. Model có **70 bảng và 116 FK**; thêm `__EFMigrationsHistory` thành **71 bảng** trong danh mục. Có **15 tham chiếu logic đơn** và **7 nhóm tham chiếu đa hình** được phân biệt với FK. Đây là cấu trúc khai báo trong mã nguồn, không phải bản kiểm kê trực tiếp một database đã triển khai.

## Mở và xem sơ đồ

1. Mở [index.html](index.html) trong Chrome, Firefox hoặc Edge. Trang mặc định mở **Tổng quan tất cả bảng**: 71 bảng trong 14 khung module và một khung hạ tầng. Có thể mở trực tiếp file trên máy, không cần khởi chạy backend.
2. Tổng quan mặc định hiện 54 FK và một tham chiếu nội bộ module. Bật **Hiện quan hệ giữa các module** để xem đủ 116 FK và 15 tham chiếu đơn. Phóng to rồi cuộn để đọc bảng; chọn module bên trái để xem từng cột và cardinality.
3. Xem riêng [SVG tổng quan](00-overview.svg), [PDF tổng quan](00-overview.pdf), hoặc [SVG đầy đủ liên kết](00-overview-all-links.svg).
4. Chọn một SVG trong danh mục bên dưới để xem riêng. Trong browser, dùng chức năng phóng to để đọc tên cột và đường nối.
5. Trong VS Code, nhấp phải file `.svg`, chọn **Open With… → Image Preview**. Có thể mở từ Explorer nếu editor đang hiển thị mã XML của SVG.
6. Xem [bảng quan hệ dạng Markdown](quan-he.md) để tra các FK và tìm nhanh trong VS Code.
7. Dùng [ef-model.json](ef-model.json) để tra đầy đủ cột, kiểu dữ liệu, nullable, PK, unique index và FK. Sơ đồ chỉ chọn các cột cần thiết để thể hiện quan hệ; các file `.dot` cùng tên giữ nguồn Graphviz của từng sơ đồ.

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

## Danh mục 71 bảng theo module

Module được xác định theo namespace entity trong backend. Sáu entity có sẵn của ASP.NET Core Identity thuộc module Identity. Mỗi bảng chỉ xuất hiện một lần trong tổng quan; `ContentReports` thuộc Posts, còn Admin chứa quyết định kiểm duyệt và trạng thái tài khoản.

| Module / sơ đồ chi tiết | Số bảng | Bảng thuộc module |
|---|---:|---|
| [Identity](module-identity.svg) | 13 | `AspNetRoleClaims`, `AspNetRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserRoles`, `AspNetUsers`, `AspNetUserTokens`, `AuthSessions`, `ExternalLoginTickets`, `PasswordResetOtps`, `RefreshTokens`, `RegistrationChallenges`, `TwoFactorLoginChallenges` |
| [Users](03-users.svg) | 2 | `UserPrivacySettings`, `UserProfiles` |
| [Friends](04-friends.svg) | 5 | `BlockedUsers`, `FriendNotifications`, `FriendRequests`, `Friendships`, `UserFollows` |
| [Posts](module-posts.svg) | 11 | `CommentReactions`, `Comments`, `ContentMentions`, `ContentReports`, `Hashtags`, `PostHashtags`, `PostMedia`, `PostReactions`, `Posts`, `PostSaves`, `PostShares` |
| [Media](07-media.svg) | 5 | `MediaAssets`, `MediaProcessingJobs`, `MediaReferences`, `ObjectDeletions`, `ProfileMediaReferences` |
| [Groups](08-groups.svg) | 6 | `GroupCoverMediaReferences`, `GroupInvites`, `GroupJoinRequests`, `GroupMembers`, `GroupRules`, `Groups` |
| [Pages](09-pages.svg) | 5 | `PageFollowers`, `PageMediaReferences`, `PageMembers`, `PageRoleInvitations`, `Pages` |
| [Messages](module-messages.svg) | 7 | `ConversationParticipants`, `ConversationReadCursors`, `Conversations`, `MessageAttachments`, `MessageNotifications`, `MessageReactions`, `Messages` |
| [Notifications](12-notifications.svg) | 3 | `Notifications`, `PushDeliveryReceipts`, `PushDevices` |
| [Admin](module-admin.svg) | 2 | `ModerationActions`, `UserModerationStates` |
| [Events](14-events.svg) | 4 | `EventCoverMediaReferences`, `EventInvitations`, `EventParticipants`, `Events` |
| [Photos](15-photos.svg) | 2 | `AlbumMedia`, `PhotoAlbums` |
| [Stories](16-stories.svg) | 4 | `Stories`, `StoryMediaReferences`, `StoryReactions`, `StoryViews` |
| [Reels](17-reels.svg) | 1 | `ReelViews` |
| [Infrastructure](18-migration-history.svg) | 1 | `__EFMigrationsHistory` |
| **Tổng** | **71** | **70 bảng EF + một bảng lịch sử migration** |

Tổng quan hiển thị tên bảng, PK và số FK để dễ nhìn toàn bộ cấu trúc. Sơ đồ từng module giữ các cột PK/FK/REF và cardinality. Bảng không có FK vẫn xuất hiện đầy đủ; các nhóm tham chiếu đa hình được giải thích trong bảng quan hệ.

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

Sơ đồ `00-overview-all-links.dot` giữ vị trí các module từ bản tổng quan. Khi render lại tệp này, dùng engine Graphviz `nop2` (tương đương `neato -n2`) để giữ bố cục đã lưu.
