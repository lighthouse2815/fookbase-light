# Mixed Home Feed và ranking V1 (M10) — historical

Tài liệu này ghi lại thiết kế M10 tại thời điểm hoàn thành. Feed V1 đã được thay thế bởi [Feed Ranking V2](feed-ranking-v2.md), là tài liệu hiện hành cho behavior và configuration production.

Feed tiếp tục là query module trên `FookbaseDbContext`; không có bảng timeline,
cache hoặc event/fan-out mới. Stories vẫn ở tray riêng. Search không đổi.

## API và nguồn nội dung

- `GET /api/feed?cursor=&limit=`: Home xếp hạng, kèm Reel gợi ý có quota.
- `GET /api/feed/following?cursor=&limit=`: organic theo `CreatedAtUtc DESC, Id DESC`.
- Cả hai yêu cầu đăng nhập; limit mặc định 20, tối đa 50. Input/cursor không hợp lệ trả 400.
- Response: `{ items, nextCursor, asOfUtc }`; frontend giữ cursor opaque.

Organic gồm Standard Profile posts và Profile Reels của mình/bạn bè, Standard
posts trong Group đang tham gia, Standard posts từ Page published đang follow.
Standard Profile posts của người lạ không phải ứng viên. Home có thêm Public
Profile Reels của người không phải bạn bè trong 7 ngày trước snapshot.

## Ranking và cấu hình

Cấu hình ở `FeedRanking` trong appsettings, override bằng biến môi trường
`FeedRanking__<Property>` theo convention hiện tại.

| Property | Mặc định | Ý nghĩa |
| --- | ---: | --- |
| OwnAffinity | 8 | Nội dung Profile của mình |
| FriendAffinity | 6 | Nội dung Profile của bạn bè |
| GroupAffinity | 4 | Group đang tham gia |
| PageAffinity | 3 | Page đang follow |
| SuggestedReelAffinity | 1 | Thứ tự bên trong nguồn Reel gợi ý |
| FreshnessHoursPerPoint | 6 | Một điểm affinity tương đương 6 giờ độ mới |
| CandidateLimitPerSource | 100 | Số ứng viên tối đa mỗi nguồn, hợp lệ 51–500 |
| OrganicItemsPerSuggestion | 4 | Tối thiểu bốn organic giữa hai Reel gợi ý |
| SuggestedReelMaxAgeDays | 7 | Tuổi tối đa Reel gợi ý tại snapshot |

Điểm khái niệm: `affinity - ageHours / FreshnessHoursPerPoint`.
Implementation dùng số nguyên ticks tương đương để tránh sai số floating point:
`affinity * FreshnessHoursPerPoint * TicksPerHour - (AsOfUtcTicks - CreatedAtUtcTicks)`.
Organic sắp theo điểm giảm dần, rồi thời gian tạo giảm dần, rồi ID giảm dần.
Ví dụ: post mới của bạn bè đứng trước post của mình đã 48 giờ; cùng thời gian,
nội dung của mình đứng trước bạn bè, Group, rồi Page.

Không dùng live reaction/comment/view counts trong thứ tự; vẫn trả engagement
cho UI. Ranking V2 có thể dùng tương tác/watch-time/completion/replay sau khi có
chiến lược snapshot phù hợp. Không thêm diversity theo tác giả/Page trong V1 vì
phải giữ keyset đơn giản, ổn định; quota Reel là ràng buộc diversity duy nhất.

## Snapshot, cursor và quota

Request đầu tạo `AsOfUtc` (độ chính xác microsecond như PostgreSQL). Mọi request
trong chuỗi chỉ lấy nội dung tạo trước hoặc đúng snapshot; refresh bắt đầu chuỗi
mới. Cập nhật content và engagement vẫn được hiển thị theo dữ liệu hiện tại.

Cursor version 1 được mã hóa và xác thực bằng ASP.NET Data Protection, bind với
viewer, Home/Following và hash cấu hình ranking. Payload giữ snapshot, frontier
organic, frontier Reel gợi ý và số organic kể từ gợi ý gần nhất. Frontier gồm
score, createdAt và ID. Sửa cursor/đổi viewer/mode/config khiến request trả 400.
Cursor cũ của Feed trước M10 không còn tương thích; client cần refresh.

Reel gợi ý chỉ xuất hiện sau ít nhất bốn organic. Không tích lũy nhiều quota để
dồn Reel vào cuối feed, không fill một feed trống bằng toàn Reel gợi ý. Quota và
hai frontier đi cùng cursor nên page size=1 hoặc thay đổi page size không làm
mất Reel đang chờ. Mỗi request có một lookahead item để xác định nextCursor.

Data Protection key ring mặc định thuộc tài khoản chạy API. Khi triển khai,
giữ key ring qua lần thay container nếu cần giữ session phân trang; nếu key mất,
cursor cũ bị từ chối an toàn và người dùng refresh. Không lưu key vào repository.

## Quyền truy cập và danh tính

- Profile: `PostVisibility` + snapshot friendship/block hai chiều mỗi request.
- Group: `GroupPostAccessService.ApplyDirectAccess`, cộng membership hiện tại;
  không lộ Group private ngoài quyền và không lấy nội dung người bị block.
- Page: `PagePostAccessService.ApplyPublishedAccess`, cộng follower hiện tại;
  block publishing manager không biến Page thành nội dung cá nhân.
- Reel: `PostVisibility` và `ReelMediaQuery` dùng chung với Reel detail/access,
  yêu cầu Video Ready, chưa xóa và đầy đủ processed/poster/duration/dimensions.

Mỗi request xét lại unfriend/block, membership, follow, publish và deletion;
cursor không cấp quyền. Không snapshot quan hệ để giữ quyền đã bị thu hồi.
Nếu quan hệ thay đổi giữa các trang, eligibility/rank có thể đổi; refresh là
cách bắt đầu thứ tự mới đầy đủ. Client loại ID trùng khi append.

Page `displayAuthor` là Page, `author.userId=null`; không serialize publisher.
Group hiển thị UserProfile kèm container identity. Profile hiển thị UserProfile.
Reel metadata dùng access paths; poster chỉ ký URL khi frontend yêu cầu preview.
Home không autoplay video, click mở Reel viewer hiện có.

## Cửa sổ ứng viên và index

Bốn query organic riêng (own, friend, Group, Page), cộng tối đa một query Reel
gợi ý. Mỗi query `AsNoTracking`, lọc keyset **trước** `Take(CandidateLimitPerSource)`.
Affinity hằng số trong từng nguồn cho phép đổi score frontier thành điều kiện
CreatedAt/Id tận dụng index; không sort score toàn bộ lịch sử trong database.
Khi merge chỉ giữ tối đa `5 * CandidateLimitPerSource` ứng viên trong bộ nhớ.
Với min window=51, mỗi nguồn đủ 50 kết quả + lookahead; lần sau dịch frontier nên
đi qua được cửa sổ đầu tiên, không âm thầm cắt lịch sử.

DTO tái sử dụng `PostsService.LoadResponsesAsync` để batch attachment, comments,
reaction summary, viewer reaction và Page display identity; profile/group/media
metadata được lấy batch. Số command không tỷ lệ với số item.

Index hiện có đã đủ cho truy vấn V1: Posts partial author/date/id,
containerType/containerId/date/id, postType/date/id, postType/author/date/id;
Group membership lookup và Page follower key; Friends/Blocks giữ index cũ.
M10 không thêm migration hoặc index trùng lặp. Cần đo EXPLAIN/latency trên dữ liệu
production trước khi bổ sung index hay thay chiến lược lấy ứng viên.

## Frontend và kiểm thử

Home/Following dùng chung FeedPage, LivePostCard tái sử dụng cho Profile/Group/Page;
FeedReelCard là poster preview tới Reel viewer. Refresh/đổi mode hủy request cũ,
xóa snapshot và cursor cũ; append chỉ nhận cùng AsOfUtc. Tạo post local refresh
feed thay vì chèn vào cursor chain cũ.

Integration tests bao phủ privacy/container identity, quota xuyên trang, cursor
tamper/viewer/mode/config, snapshot, ranking/ties, relationship revocation và
610 organic items (122 mỗi loại) qua nhiều cửa sổ; đếm SQL so sánh limit 5/50.
Kiểm thử hồi quy toàn bộ backend, fresh migration, pending model,
production Docker/health và build/lint cả ba frontend thuộc checklist bàn giao.

Ẩn nội dung (FeedHiddenContent) được hoãn để M10 không phát sinh persistence mới.
