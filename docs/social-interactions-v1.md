# Social interactions V1 (M11)

M11 thêm Save, Share, mention và hashtag vào Posts/Reels mà không tạo bảng timeline, copy nội dung hay copy media. Mọi đọc lại áp dụng ACL của nội dung gốc ở thời điểm request; cursor chỉ là vị trí phân trang, không mang quyền truy cập.

## Data Protection và cursor

Saved posts và hashtag timeline dùng keyset cursor ASP.NET Core Data Protection, bind theo người xem và purpose riêng (`saved`, `hashtag:<normalized-tag>`). Feed cũng dùng key ring chung. Production bắt buộc đặt `DataProtection__KeyRingPath`; Compose mount volume `data-protection-keys` tại `/var/fookbase/data-protection-keys`. Hướng dẫn backup và smoke test sau khi tái tạo API có tại [deployment.md](deployment.md#data-protection-key-ring).

## Save

| Method | Endpoint | Quyền |
| --- | --- | --- |
| POST | `/api/posts/{postId}/save` | Bearer JWT, đang xem được post |
| DELETE | `/api/posts/{postId}/save` | Bearer JWT, idempotent |
| GET | `/api/posts/saved?cursor=&limit=` | Bearer JWT, keyset saved time |

`PostSave` có khóa chính duy nhất `(UserId, PostId)`, nên gọi save lại không tạo bản ghi thứ hai. Saved page sắp `SavedAtUtc DESC, PostId DESC`; limit mặc định 20, tối đa 50. Những bản ghi vẫn lưu nhưng bài gốc đã bị xóa, unfriend/block, rời Group, unfollow/unpublish Page hoặc đổi privacy sẽ không được trả về.

## Share

| Method | Endpoint | Quyền |
| --- | --- | --- |
| POST | `/api/posts/{postId}/shares` | Bearer JWT, xem được bài gốc và có quyền đích |

Body là `{ destinationType, destinationId, caption? }`, với destination type `profile`, `group` hoặc `page`. Profile chỉ là chính người chia sẻ; Group dùng quyền tạo post của active member; Page dùng quyền Owner, Admin hoặc Editor.

`PostShare` chỉ giữ tham chiếu `OriginalPostId`, actor, destination, caption và thời gian. Nó không sao chép nội dung, attachment/media, reaction hoặc comment của bài gốc. Share chỉ được tạo nếu actor đang xem được bài gốc. Khi đọc Feed, bài gốc được kiểm tra lại bằng ACL hiện thời nên share sẽ biến mất nếu bài gốc bị xóa, đổi privacy, bị block/unfriend, hay mất quyền Group/Page. Profile share của mình/bạn bè, Group share của member và Page share của follower là Feed event có keyset position theo thời điểm/ID của share.

Tạo share queue `PostShared` tới tác giả bài gốc qua notification subsystem; cơ chế queue không tạo self-notification. Page destination serialize Page identity, không lộ manager đã thao tác.

## Mentions

Mention hợp lệ là `@username` trong content Post, Reel caption hoặc Comment. Parser giữ `StartIndex`/`Length` và user đích trong `ContentMentions`; frontend render theo range này, không chèn HTML. Username không tồn tại, target không còn xem được nội dung, hoặc block actor đều không tạo relationship mention. Tối đa 20 syntax mention đầu tiên được xét trên một content; cùng target ở nhiều vị trí vẫn được lưu theo range, nhưng notification chỉ một lần.

Sau create/update, relation được đồng bộ lại. Chỉ target mới xuất hiện so với trạng thái trước edit nhận `PostMention` hoặc `CommentMention`; self-notification được NotificationService chặn. API response Post, Comment, Reel và Feed trả metadata mention để UI liên kết tới `/profile/{userId}`.

## Hashtags

`#tag` chấp nhận Unicode letter/digit/underscore, dài 1–50; tên chuẩn hóa lowercase trong `Hashtags.NormalizedName` duy nhất, còn `DisplayName` giữ dạng đầu tiên. Mỗi Post/Reel caption có tối đa 20 hashtag khác nhau và relation nằm ở `PostHashtags`; edit thay các relation cũ bằng tập mới.

| Method | Endpoint | Quyền |
| --- | --- | --- |
| GET | `/api/hashtags/{tag}/posts?cursor=&limit=` | Optional JWT, ACL từng item |

Timeline sắp `CreatedAtUtc DESC, Id DESC`, dùng cursor protected và quét tiếp khi gặp item không còn nhìn thấy. Global Search `type=all` bổ sung `hashtags` prefix match (query có hoặc không có `#`). Web có route `/hashtag/:tag`, text renderer link hashtag và Search Page hiển thị kết quả hashtag; `/saved` hiển thị saved list.

## Query performance và migration

Migration runtime `20260912093027_AddSocialInteractionsV1` tạo `PostSaves`, `PostShares`, `ContentMentions`, `Hashtags` và `PostHashtags`, cùng index:

- `PostSaves(UserId, SavedAtUtc, PostId)` cho saved keyset;
- `PostShares(DestinationType, DestinationId, DeletedAtUtc, CreatedAtUtc, Id)` cho Feed share candidate và `(OriginalPostId, DeletedAtUtc)`;
- `ContentMentions(SourceType, SourceId, MentionedUserId)` và recipient lookup;
- `Hashtags(NormalizedName)` unique, `PostHashtags(HashtagId, PostId)`.

Chạy migration theo deployment runbook trước khi đổi traffic. Cần đo `EXPLAIN` và p95 thật trước khi thêm denormalization/cache; M11 cố ý không có fan-out, ranking theo engagement mới, topic moderation, trending/related tags, share edit hay share delete UI/API.
