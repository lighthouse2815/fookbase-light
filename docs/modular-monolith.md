# Quyết định kiến trúc nguyên khối phân mô-đun

## Phạm vi

Fookbase chạy trong một tiến trình ASP.NET Core tên `Fookbase.Api` trên cổng 5000. Identity, Users, Friends, Feed, Groups, Messages, Notifications, Posts và Media là các mô-đun mã nguồn riêng biệt, nhưng không được triển khai thành các dịch vụ độc lập.

`Fookbase.Api` là điểm khởi tạo và cấu hình duy nhất của backend. Ứng dụng đăng ký `FookbaseDbContext` một lần, sau đó đăng ký từng mô-đun qua `AddIdentityModule`, `AddUsersModule`, `AddFriendsModule`, `AddFeedModule`, `AddGroupsModule`, `AddMessagesModule`, `AddNotificationsModule`, `AddPostsModule` và `AddMediaModule`. Mã nguồn của các mô-đun nằm trong `backend/Fookbase.Src/Main/Code/Modules/<Module>`; endpoint HTTP và cấu hình entity vẫn nằm trong thư mục của mô-đun tương ứng. Migration đang dùng và bản chụp mô hình dữ liệu nằm trong `Code/Persistence/Migrations`.

Khi chạy, ứng dụng phụ thuộc vào PostgreSQL và Cloudinary. Hệ thống không có API gateway, RabbitMQ, cơ chế tìm dịch vụ, giao dịch phân tán hoặc giao tiếp HTTP giữa các mô-đun.

## Ranh giới và cách giao tiếp giữa các mô-đun

Mỗi mô-đun giữ các thư mục `Entities`, `Data`, `Services` và `Endpoints` trong cùng project API. Tất cả service dùng chung một `FookbaseDbContext` theo phạm vi request; ranh giới mô-đun dùng để tổ chức mã nguồn, không phải để chia database. Khi xử lý báo cáo người dùng, hệ thống xác minh tài khoản Identity tồn tại. Trước khi xóa media, hệ thống kiểm tra các tham chiếu đang dùng trên hồ sơ.

Việc phối hợp giữa các mô-đun được thể hiện rõ trong mã nguồn và thực hiện đồng bộ. Lớp điều phối nằm trong mô-đun sở hữu endpoint, chẳng hạn `Modules/Identity/Services/RegistrationUseCase`, `Modules/Posts/Services/PostsUseCase` và `Modules/Admin/Services/AdministrationUseCase`. `RegistrationUseCase` lưu tài khoản đăng nhập, refresh token và hồ sơ người dùng trong một giao dịch. `PostsUseCase` lấy trạng thái quan hệ từ Friends để kiểm tra quyền xem, rồi lưu bài viết cùng các tham chiếu media trong một giao dịch. Thay đổi ảnh hồ sơ và tham chiếu của ảnh cũng dùng một giao dịch. Messages dùng `FriendsService` để kiểm tra quyền truy cập hội thoại trực tiếp và dùng SignalR tại `/hubs/messages` để cập nhật client. Notifications dùng chung context: Friends và Posts ghi thông báo bền vững trong thao tác lưu hiện có, sau đó cố gắng phát thông báo qua `/hubs/notifications` sau khi giao dịch hoàn tất. Đây là các lời gọi C# trong cùng tiến trình, không phải request HTTP.

Feed là mô-đun truy vấn chỉ đọc. Nó lấy trạng thái quan hệ hiện tại từ Friends, áp dụng cùng quy tắc `PostVisibility` như khi đọc trực tiếp bài viết, rồi tải theo lô tác giả, media và dữ liệu tương tác cho một trang phân trang bằng cursor. Feed không sở hữu entity, bảng dữ liệu, cache hay tác vụ phát tán chạy nền.

Groups quản lý nhóm, vai trò thành viên, yêu cầu tham gia, lời mời và quy tắc nhóm. Groups dùng chung context với Posts, Media và Notifications: bài viết nhóm dùng bảng `Posts` hiện có thông qua `Post.ContainerType` và `ContainerId`. `GroupPostAccessService` là nơi kiểm tra tập trung quyền thành viên và quyền riêng tư khi đọc bài viết, bình luận, cảm xúc và media. Ảnh bìa nhóm được bảo vệ bằng cơ chế tham chiếu media hiện có; lời mời và yêu cầu tham gia nhóm riêng tư được duyệt sẽ tạo thông báo bền vững, rồi hệ thống cố gắng phát thông báo sau khi lưu.

## Không dùng lớp đồng bộ dữ liệu qua thông điệp

Ứng dụng không có lớp nhắn tin giữa các mô-đun, hợp đồng sự kiện tích hợp, outbox, inbox, bộ phát sự kiện, worker cập nhật mô hình dữ liệu phục vụ đọc hoặc dịch vụ outbox chạy nền. Posts đọc dữ liệu quan hệ hiện tại qua `FriendsService`; Media sở hữu metadata và các tham chiếu của tệp đính kèm. Các lời gọi trực tiếp phù hợp với ứng dụng chạy trong một tiến trình này.

## Lưu trữ dữ liệu

`fookbase_db` là database PostgreSQL duy nhất khi chạy và `FookbaseDbContext` là EF Core context duy nhất. Context ánh xạ các bảng của Identity (`AspNet*`, `RefreshTokens`), Users (`UserProfiles`), Friends, Groups, Messages, Notifications, Posts và Media mà không đổi tên bảng chỉ để thống nhất hình thức. Feed không có bảng lưu trữ riêng. Lược đồ hợp nhất có một bảng `__EFMigrationsHistory`, không có bảng Inbox hoặc Outbox và không có tên bảng bị trùng.

`Code/Persistence/Migrations/20260910143327_InitialFookbase.cs` khởi tạo database mới đầy đủ; `20260910154744_AddNotifications.cs` thêm `Notifications` và `CommentReactions`; `20260910162758_AddFeedPostIndex.cs` thêm index một phần cho bài viết còn hoạt động, phục vụ phân trang theo khóa của Feed; `20260910165428_AddGroups.cs` thêm các bảng Groups và cập nhật bài viết hồ sơ đã có với `ContainerType=Profile` và `ContainerId=AuthorUserId`. Các migration sau đó và bản chụp mô hình hiện tại đều nằm trong cùng thư mục migration đang dùng. Quy ước được ghi trong [migration-history.md](migration-history.md).

Ứng dụng đang chạy không dùng outbox hoặc inbox. Quy trình bền vững còn lại là bảng `ObjectDeletions` của Media, được `ObjectDeletionWorker` xử lý để thử lại việc xóa trên Cloudinary; quy trình này không phục vụ việc phối hợp giữa các mô-đun.

## Tương thích

Các route công khai vẫn nằm dưới `/api/auth`, `/api/users`, `/api/friends`, `/api/feed`, `/api/groups`, `/api/messages`, `/api/notifications`, `/api/posts` và `/api/media`, được phục vụ tại `http://localhost:5000`. Các hub SignalR nằm tại `/hubs/messages` và `/hubs/notifications`. JWT và ASP.NET Core Identity không thay đổi; endpoint tiếp tục lấy mã định danh người thực hiện từ claim `sub` của JWT.

Media dùng kiểu phân phối `authenticated` của Cloudinary. Client tải lên bằng biểu mẫu POST có chữ ký; `PostsUseCase` lấy URL đọc có chữ ký qua Media service trong cùng tiến trình. Media tiếp tục chịu trách nhiệm kiểm tra chủ sở hữu, tham chiếu, vòng đời và chữ ký.
