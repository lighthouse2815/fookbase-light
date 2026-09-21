# Fookbase Light

Fookbase Light V1 là một modular monolith cho mạng xã hội. Toàn bộ domain chạy trong một ASP.NET Core process tại cổng `5000`, với một PostgreSQL database (`fookbase_db`), Cloudinary và SignalR.

Code nghiệp vụ được chia theo feature module trong một project backend duy nhất. Mỗi luồng giữ đơn giản theo `Endpoint -> module coordinator (khi cần phối hợp) -> Service -> DbContext`.

Chi tiết về ranh giới module và kế hoạch hợp nhất database được ghi tại [docs/modular-monolith.md](docs/modular-monolith.md).

## Kiến trúc

```text
React frontend
      |
      v
Fookbase.Api :5000
  |-- Identity, account security and privacy
  |-- Users, profiles, friends and follows
  |-- Posts, comments, reactions, saves, shares and media
  |-- Feed Ranking V2, search and notifications (SignalR)
  |-- Messenger / Zola Light (SignalR)
  |-- Groups, Pages, Reels and Stories
  |-- Events, photos/albums, memories and birthdays
  `-- Moderation and administration
      |
      |-- PostgreSQL
      `-- Cloudinary
```

Backend chỉ có một entry point: `backend/Fookbase.Src/Main`. Các route cũ dưới `/api/*` được giữ nguyên nên frontend/client không cần đổi base URL.

Toàn bộ persistence runtime dùng duy nhất `FookbaseDbContext` và PostgreSQL database `fookbase_db`. Module vẫn giữ entity, configuration và service trong folder riêng; chỉ DbContext và migration history được hợp nhất.

Ba React app được triển khai độc lập: `frontend/web`, `frontend/zola-light` và `frontend/admin`. V1 chỉ hỗ trợ **một API instance**; không có SignalR/presence horizontal scaling. Xem chi tiết tại [kiến trúc](docs/modular-monolith.md), [vận hành production](docs/production-deployment.md), [Zola Light](docs/zola-light-v1.md), [Feed Ranking V2](docs/feed-ranking-v2.md), [privacy/security](docs/privacy-security-v1.md) và [moderation](docs/moderation-v1.md).

## Yêu cầu

- .NET SDK 10
- Node.js và npm
- Docker Engine với Docker Compose plugin

Từ thư mục gốc repository (thư mục chứa `README.md` và `compose.yml`), tạo cấu hình development:

```bash
cp .env.example .env
```

Các credential mẫu chỉ dành cho local development. Hãy thay password và JWT signing key trước khi dùng ở môi trường khác.

## Khởi động backend

### Cách 1: Chạy backend bằng Docker Compose

Mở terminal tại **thư mục gốc repository**. Sau khi điền `.env`, chạy PostgreSQL và API:

```bash
docker compose up --build -d
docker compose ps
```

Compose chỉ lấy secrets từ `.env`; Docker image không chứa `.env` hoặc credential. Điền
`Cloudinary__CloudName`, `Cloudinary__ApiKey` và `Cloudinary__ApiSecret` trong `.env` trước
khi chạy. Để gửi OTP email, đặt đầy đủ `Email__*` và `Email__Enabled=true`; Compose truyền
các biến này vào API. Local compose tự apply migration khi API start. API chạy tại
<http://localhost:5000>. Cloudinary phải cho phép origin của các frontend dùng upload trực tiếp.

Vẫn tại thư mục gốc, xem log backend và kiểm tra health:

```bash
docker compose logs -f api
```

Mở terminal khác tại thư mục gốc để chạy:

```bash
curl -fsS http://localhost:5000/health/live
curl -fsS http://localhost:5000/health/ready
```

`/health` vẫn là liveness-compatible endpoint. `/health/live` chỉ xác nhận process sống;
`/health/ready` yêu cầu PostgreSQL và Cloudinary sẵn sàng.

### Cách 2: Chạy backend trực tiếp bằng .NET SDK

Mở terminal tại **thư mục gốc repository**. Chỉ chạy PostgreSQL bằng Docker Compose; nếu API
container đang chạy, dừng nó để tránh trùng cổng `5000`:

```bash
docker compose up -d postgres
docker compose stop api
```

Từ thư mục gốc, chạy script sau. Script tự nạp `.env`, bật migration khi khởi động cho môi
trường local và chạy API; không cần `source` lại sau khi mở terminal mới. Connection string
trong `.env.example` dùng `localhost:5432` cho PostgreSQL trên Docker; kiểm tra lại giá trị
này nếu bạn đã đổi cổng.

```bash
bash scripts/run-backend.sh
```

Nếu muốn chạy thủ công, vẫn đứng tại thư mục gốc và nạp `.env` trước khi gọi `dotnet run`:

```bash
set -a
source .env
set +a
Database__ApplyMigrationsOnStartup=true dotnet run --project backend/Fookbase.Src/Main/Fookbase.Api.csproj --launch-profile http
```

Khi đứng trong `backend/Fookbase.Src/Main`, thay `source .env` bằng
`source ../../../.env` rồi chạy `Database__ApplyMigrationsOnStartup=true dotnet run`.

API chạy tại <http://localhost:5000>. Dùng các lệnh `curl` ở trên trong terminal khác để kiểm tra.
Bạn cũng có thể chạy script từ thư mục `backend/Fookbase.Src/Main` bằng
`bash ../../../scripts/run-backend.sh`.

Để mở quyền quản trị cho một tài khoản development, đặt `Admin__BootstrapEmail` thành email
của tài khoản đó trước khi đăng ký hoặc đăng nhập. Hệ thống sẽ tự gán role `Admin` vào lần
phát hành token kế tiếp; không đặt biến này ở môi trường production nếu chưa có quy trình
quản lý role riêng.

## Chạy frontend

Từ thư mục gốc repository, chạy frontend trong terminal riêng:

```bash
cd frontend/web
npm ci
npm run dev
```

Frontend chạy tại <http://localhost:5173>.

## Trợ lý AI

Fookbase Web có trang **Trợ lý AI** tại `/ai-chat`. Tính năng mặc định tắt. Để bật trên API,
đặt các biến server-side trong `.env` rồi khởi động lại Compose:

```bash
AiChat__Enabled=true
AiChat__ApiKey=<openai-api-key>
# Tùy chọn; mặc định là gpt-5-mini.
AiChat__Model=gpt-5-mini
```

Khóa tuyệt đối không được đặt trong `VITE_*` hay source frontend. API yêu cầu người dùng đăng
nhập, giới hạn mặc định 10 lượt gửi/phút mỗi tài khoản, giữ tối đa 10 lượt ngữ cảnh trên client
và gửi `store: false` cho Responses API. Có thể điều chỉnh các giới hạn bằng biến
`RateLimiting__AiChat__*` và `AiChat__Maximum*` trong `.env`.

Chạy web Admin riêng:

```bash
cd frontend/admin
npm ci
npm run dev
```

Admin Center chạy tại <http://localhost:5174>. Khi dùng local, thêm origin này vào
`Cors__AllowedOrigins__1` (đã có sẵn trong `.env.example`).

Chạy Zola Light riêng:

```bash
cd frontend/zola-light
npm ci
npm run dev
```

Zola Light chạy tại <http://localhost:5175>. Khi dùng local, thêm origin này vào
`Cors__AllowedOrigins__2` (đã có sẵn trong `.env.example`).

## Build và test

```bash
dotnet restore FookbaseLight.sln
dotnet build FookbaseLight.sln --no-restore
bash scripts/test-backend.sh
```

`scripts/test-backend.sh` là regression backend đầy đủ chuẩn: một project integration test chứa source trong `Code`, chia tiếp thành sáu thư mục `Identity`, `Users`, `Friends`, `Messages`, `Media` và `Posts`. Script lọc các nhóm theo namespace và chạy tuần tự trên PostgreSQL database riêng. Không dùng `dotnet test FookbaseLight.sln` làm full integration regression vì các nhóm dùng chung database có thể ảnh hưởng nhau khi chạy đồng thời. Direct solution test vẫn phù hợp cho kiểm tra không-integration có phạm vi rõ ràng.

Mỗi frontend dùng npm và package-lock riêng. Kiểm tra đầy đủ frontend:

```bash
for app in web zola-light admin; do
  (cd "frontend/$app" && npm ci && npm run lint && npm run build)
done
```

Nếu máy chưa có .NET SDK 10, có thể build bằng container:

```bash
docker run --rm --user "$(id -u):$(id -g)" \
  -e DOTNET_CLI_HOME=/tmp \
  -e NUGET_PACKAGES=/workspace/.nuget/packages \
  -v "$PWD:/workspace" -w /workspace \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet restore FookbaseLight.sln

docker run --rm --user "$(id -u):$(id -g)" \
  -e DOTNET_CLI_HOME=/tmp \
  -e NUGET_PACKAGES=/workspace/.nuget/packages \
  -v "$PWD:/workspace" -w /workspace \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet build FookbaseLight.sln --no-restore
```

## API

### Authentication

| Method | Endpoint | Authentication |
| --- | --- | --- |
| POST | `/api/auth/register` | Không |
| POST | `/api/auth/login` | Không |
| GET | `/api/auth/providers` | Không |
| GET | `/api/auth/google/start?client=web\|zola-light` | Không |
| POST | `/api/auth/google/exchange` | Không |
| POST | `/api/auth/google/link` | Không |
| POST | `/api/auth/refresh` | Không |
| POST | `/api/auth/logout` | Bearer JWT |
| GET | `/api/auth/me` | Bearer JWT |

JWT signing key chỉ được đọc từ `Jwt__SigningKey`. Refresh token raw chỉ trả cho client; database lưu SHA-256 hash.

Google OAuth là tùy chọn và chỉ bật khi `GoogleAuthentication__Enabled=true`. Khai báo callback duy nhất tại Google Cloud là `https://<api-host>/signin-google`; API sau đó trả về đúng trang `/login` của Fookbase Web hoặc Zola Light bằng một completion code ngắn hạn, dùng một lần. `exchange` và `link` nhận `{ code, client }`, với `client` là `web` hoặc `zola-light`; `link` yêu cầu thêm mật khẩu Fookbase khi email đã thuộc một tài khoản hiện có. Access token và refresh token không bao giờ nằm trong URL redirect.

### Users

| Method | Endpoint | Authentication |
| --- | --- | --- |
| GET | `/api/users/{userId}` | Không |
| GET | `/api/users/me` | Bearer JWT |
| PATCH | `/api/users/me` | Bearer JWT |

### Friends

| Method | Endpoint |
| --- | --- |
| POST | `/api/friends/requests/{userId}` |
| DELETE | `/api/friends/requests/{requestId}` |
| POST | `/api/friends/requests/{requestId}/accept` |
| POST | `/api/friends/requests/{requestId}/decline` |
| GET | `/api/friends/requests/incoming` |
| GET | `/api/friends/requests/outgoing` |
| DELETE | `/api/friends/{userId}` |
| GET | `/api/friends` |
| GET | `/api/friends/status/{userId}` |
| GET | `/api/friends/mutual/{userId}` |
| GET | `/api/friends/suggestions?cursor=&limit=` |
| GET | `/api/friends/notifications/unread` |
| POST | `/api/friends/notifications/{notificationId}/read` |
| POST | `/api/friends/blocks/{userId}` |
| DELETE | `/api/friends/blocks/{userId}` |
| GET | `/api/friends/blocks` |

Tất cả Friends endpoint yêu cầu Bearer JWT. Collection endpoint dùng offset pagination, `limit` mặc định 20 và tối đa 100.

### Follows và sinh nhật

| Method | Endpoint | Authentication |
| --- | --- | --- |
| POST/DELETE | `/api/users/{userId}/follow` | Bearer JWT |
| GET | `/api/users/{userId}/followers?cursor=&limit=` | Bearer JWT |
| GET | `/api/users/{userId}/following?cursor=&limit=` | Bearer JWT |
| GET | `/api/users/{userId}/friends?offset=&limit=` | Bearer JWT |
| GET | `/api/birthdays/today` | Bearer JWT |
| GET | `/api/birthdays/upcoming?days=` | Bearer JWT |

Follow dùng cursor pagination; danh sách sinh nhật chỉ trả các hồ sơ mà người xem được phép thấy theo quan hệ và cài đặt riêng tư ngày sinh.

### Messages

| Method | Endpoint | Authentication |
| --- | --- | --- |
| POST | `/api/messages/conversations/{userId}` | Bearer JWT, bạn bè |
| GET | `/api/messages/conversations` | Bearer JWT |
| GET | `/api/messages/conversations/{conversationId}/messages` | Bearer JWT, thành viên |
| POST | `/api/messages/conversations/{conversationId}/read` | Bearer JWT, thành viên |
| POST | `/api/messages/conversations/{conversationId}/messages` | Bearer JWT, thành viên |
| GET | `/api/messages/notifications` | Bearer JWT |

Tin nhắn chỉ được gửi giữa bạn bè không bị block. History dùng keyset pagination: request đầu không có `before` trả trang mới nhất; dùng `nextCursor` làm giá trị `before` để tải các tin cũ hơn. GET history không thay đổi trạng thái đã đọc; client xác nhận mốc đọc bằng `POST .../read` với `lastReadMessageId`. Read cursor được lưu theo thành viên conversation để sẵn sàng mở rộng conversation nhiều thành viên trong tương lai. Thông báo tin nhắn chưa đọc được lưu ở `MessageNotifications` và cập nhật realtime qua SignalR tại `/hubs/messages`.

### Notifications

| Method | Endpoint | Authentication |
| --- | --- | --- |
| GET | `/api/notifications?before={cursor}&limit={limit}` | Bearer JWT |
| GET | `/api/notifications/unread-count` | Bearer JWT |
| POST | `/api/notifications/{notificationId}/read` | Bearer JWT, recipient |
| POST | `/api/notifications/read-all` | Bearer JWT |

Thông báo tổng quát được lưu trong bảng `Notifications`, newest-first bằng keyset cursor
`CreatedAtUtc + Id`, và chỉ recipient có thể đọc/đánh dấu đã đọc. Các event hiện có là friend
request/acceptance, post reaction/comment, comment reaction, group invite và private-group join
approval; hành động của chính recipient
không sinh notification. Event realtime dùng SignalR tại `/hubs/notifications`. Message badge
và notification badge là hai count độc lập; general notification không được tạo cho chat message.

### Feed

| Method | Endpoint | Authentication |
| --- | --- | --- |
| GET | `/api/feed?cursor={cursor}&limit={limit}` | Bearer JWT |

Home Feed V2 chỉ gồm post hợp lệ của người xem và bạn bè hiện tại: post của chính người xem
có mọi privacy, còn post của bạn chỉ có `public` hoặc `friends`. Post của non-friend, post
`onlyMe` của người khác, post bị xóa hoặc bị block không xuất hiện. Trang dùng keyset cursor
`CreatedAtUtc + Id` theo newest-first, limit mặc định 20/tối đa 50. Response đã batch author,
media metadata, comment/reaction counts và viewer reaction; frontend tiếp tục lấy media URL ngắn
hạn qua endpoint media access đã được authorize.

### Groups

| Method | Endpoint | Authentication |
| --- | --- | --- |
| POST | `/api/groups` | Bearer JWT |
| GET/PATCH/DELETE | `/api/groups/{groupId}` | GET tùy theo privacy; mutation theo role |
| GET | `/api/groups/mine` | Bearer JWT, keyset cursor |
| GET | `/api/groups/discover?query=&cursor=&limit=` | Public groups, keyset cursor |
| POST | `/api/groups/{groupId}/join` | Bearer JWT |
| POST | `/api/groups/{groupId}/leave` | Bearer JWT, member |
| GET | `/api/groups/{groupId}/members` | Theo group privacy, keyset cursor |
| GET | `/api/groups/{groupId}/join-requests` | Moderator/Admin/Owner |
| POST | `/api/groups/{groupId}/join-requests/{requestId}/approve|decline` | Moderator/Admin/Owner |
| GET | `/api/groups/invites/mine` | Bearer JWT, keyset cursor |
| POST | `/api/groups/{groupId}/invites` | Bearer JWT, member |
| POST | `/api/groups/{groupId}/invites/{inviteId}/accept|decline` | Bearer JWT, invitee |
| PATCH | `/api/groups/{groupId}/members/{userId}/role` | Owner |
| DELETE | `/api/groups/{groupId}/members/{userId}` | Owner; Admin removes ordinary member |
| GET/POST | `/api/groups/{groupId}/rules` | Read theo privacy; Owner/Admin create |
| PATCH/DELETE | `/api/groups/{groupId}/rules/{ruleId}` | Owner/Admin |
| GET/POST | `/api/groups/{groupId}/posts?cursor=&limit=` | Read theo privacy; member creates |
| DELETE | `/api/groups/{groupId}/posts/{postId}` | Moderator/Admin/Owner |

Tạo group và membership `Owner` được commit trong một transaction. Public group join ngay;
private group tạo join request. Owner phải transfer ownership hoặc xóa group trước khi rời.
Group post tái sử dụng Posts, Comments, Reactions, Reports và private Media; post của Group không
được đưa vào Home Feed V2. Cover image chỉ dùng ready image do actor sở hữu, có reference riêng
để không thể xóa media còn đang được group active tham chiếu.

### Posts

| Method | Endpoint | Authentication |
| --- | --- | --- |
| POST | `/api/posts` | Bearer JWT |
| PUT/DELETE | `/api/posts/{postId}` | Bearer JWT, tác giả |
| GET | `/api/posts/{postId}` | Tùy chọn |
| GET | `/api/posts/feed` | Bearer JWT |
| GET | `/api/posts/users/{userId}` | Tùy chọn |
| POST | `/api/posts/{postId}/comments` | Bearer JWT |
| GET | `/api/posts/{postId}/comments` | Tùy chọn |
| PUT/DELETE | `/api/posts/comments/{commentId}` | Bearer JWT, tác giả |
| PUT/DELETE | `/api/posts/comments/{commentId}/reaction` | Bearer JWT |
| PUT/DELETE | `/api/posts/{postId}/reaction` | Bearer JWT |
| GET | `/api/posts/{postId}/reactions?type=&offset=&limit=` | Bearer JWT, theo quyền xem bài viết |
| GET | `/api/posts/{postId}/media/{mediaId}/access` | Bearer JWT |

Privacy hợp lệ gồm `public`, `friends`, `onlyMe`; reaction gồm `like`, `love`, `haha`, `wow`, `sad`, `angry`. Danh sách người thả cảm xúc hỗ trợ lọc `type`, phân trang `offset`/`limit`, và trả trạng thái quan hệ của người xem với từng tài khoản.
Profile posts giữ `ContainerType=Profile` và `ContainerId=AuthorUserId`; Group posts dùng
`ContainerType=Group`. `GET /api/posts/{postId}` cùng comment/reaction/media access luôn kiểm
tra Group membership/privacy khi post nằm trong Group.

### Albums và memories

| Method | Endpoint | Authentication |
| --- | --- | --- |
| POST | `/api/albums` | Bearer JWT |
| GET/PATCH/DELETE | `/api/albums/{albumId}` | GET tùy chọn; mutation là chủ album Custom |
| GET | `/api/albums/{albumId}/media?cursor=&limit=` | Tùy chọn, theo privacy album |
| POST | `/api/albums/{albumId}/media` | Bearer JWT, chủ album Custom |
| GET | `/api/albums/{albumId}/media/{mediaId}` | Tùy chọn, theo privacy album |
| GET | `/api/albums/{albumId}/media/{mediaId}/access` | Tùy chọn, redirect URL được authorize |
| PATCH/DELETE | `/api/albums/{albumId}/media/{mediaId}` | Bearer JWT, chủ album Custom |
| GET | `/api/users/{userId}/albums?cursor=&limit=` | Tùy chọn, theo privacy album |
| GET | `/api/memories/today` | Bearer JWT, chỉ ký ức của chính người xem |

Album có loại `custom`, `profilePictures`, `coverPhotos`, `timelinePhotos`; album hệ thống được tạo khi cần và không thể chỉnh sửa trực tiếp. Albums dùng cursor pagination; memories không tạo bản sao bài viết hay thông báo tự động.

### Media

| Method | Endpoint | Authentication |
| --- | --- | --- |
| POST | `/api/media/uploads` | Bearer JWT |
| POST | `/api/media/{mediaId}/complete` | Bearer JWT, chủ sở hữu |
| GET | `/api/media/{mediaId}` | Bearer JWT, chủ sở hữu |
| DELETE | `/api/media/{mediaId}` | Bearer JWT, chủ sở hữu |

Upload dùng biểu mẫu POST có chữ ký trực tiếp tới Cloudinary với kiểu phân phối `authenticated`. Posts lấy URL đọc có chữ ký bằng lời gọi C# trực tiếp tới Media module; endpoint HTTP nội bộ và shared service token cũ đã được loại bỏ.

### Admin

| Method | Endpoint | Chức năng |
| --- | --- | --- |
| GET | `/api/admin/dashboard` | Thống kê users, posts và reports chờ xử lý |
| GET | `/api/admin/users` | Danh sách tài khoản, hỗ trợ `query`, `offset`, `limit` |
| PATCH | `/api/admin/users/{userId}/status` | Bật/tắt tài khoản thường |
| GET | `/api/admin/reports` | Danh sách reports, lọc theo `status` |
| PATCH | `/api/admin/reports/{reportId}/status` | Đánh dấu `reviewed`, `resolved` hoặc `dismissed` |
| DELETE | `/api/admin/posts/{postId}` | Gỡ bài viết vi phạm |

Tất cả endpoint Admin yêu cầu JWT có role `Admin`.

## Phối hợp module

Khi một API cần nhiều service, endpoint gọi coordinator trong module sở hữu endpoint:
`Modules/Identity/Services/RegistrationUseCase`, `Modules/Posts/Services/PostsUseCase`, hoặc
`Modules/Admin/Services/AdministrationUseCase`. Đăng ký tạo Identity và profile trong một
transaction; Posts lấy quan hệ hiện tại từ Friends và đồng bộ attachment với Media. Không có
endpoint nào truy cập `DbContext` trực tiếp hoặc điều phối nhiều service.

## Tạo migration mới

```bash
dotnet tool run dotnet-ef migrations add MigrationName \
  --project backend/Fookbase.Src/Main \
  --startup-project backend/Fookbase.Src/Main \
  --context FookbaseDbContext \
  --output-dir Code/Persistence/Migrations
```

Runtime migration và snapshot nằm ở `backend/Fookbase.Src/Main/Code/Persistence/Migrations`.
Xem [docs/migration-history.md](docs/migration-history.md) để biết quy ước migration hiện hành.
