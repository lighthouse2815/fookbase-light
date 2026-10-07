# Rà soát khả năng dọn và tối giản dự án — 07/10/2026

Phạm vi: backend ASP.NET Core, 19 module nghiệp vụ, 5 client (`web`, `admin`, `zola-light`, `mobile`, `zola-mobile`), test, script, dependency, CI và tài liệu. Mốc Git: `6fe0b083`. Kiểm tra thực hiện trên workspace hiện tại, gồm các thay đổi chưa commit ở request Posts, entity Post, UserProfileService và test validation Posts. Báo cáo không đưa những thay đổi đó vào commit.

Đã kiểm kê 1.397 file tracked, đọc cấu hình và các luồng chính, chạy Knip 6.40.0 trên cả 5 client, đối chiếu import/route, so sánh file bằng SHA-256 và chạy các check được ghi bên dưới. Đây là rà soát cấu trúc và các điểm nóng, không phải chứng nhận mọi dòng code hoặc mọi luồng giao diện đã được kiểm thử.

Kết luận: có code không dùng và logic lặp đáng dọn. Cơ hội giảm code tự quản lý lớn nhất nằm ở frontend. Backend đã tận dụng EF Core, ASP.NET Core Identity, SignalR và Cloudinary; nên tiếp tục dùng những phần có sẵn này. Một số lỗi truy vấn/phân trang cần ưu tiên trước khi đổi thư viện.

## 1. Các vấn đề cần ưu tiên

### Truy vấn song song trên cùng DbContext

- [PhotosService.cs](../backend/Fookbase.Src/Code/Modules/Photos/Services/PhotosService.cs), dòng 64: `Task.WhenAll(page.Select(...ToSummaryAsync...))`; `ToSummaryAsync` dùng cùng `db` để chạy `CountAsync` và `FirstOrDefaultAsync` ở dòng 171.
- [EventsService.cs](../backend/Fookbase.Src/Code/Modules/Events/Services/EventsService.cs), dòng 254 và 405: `Task.WhenAll` gọi các hàm dựng response, trong đó lại truy vấn cùng `db`.

Danh sách có nhiều phần tử có thể phát sinh lỗi khi các truy vấn chồng nhau. Microsoft xác định một DbContext không hỗ trợ nhiều thao tác song song. Đây là nhận định từ source và quy tắc EF Core; lượt rà soát này chưa dựng một test riêng tái hiện từng endpoint. Hướng sửa: batch dữ liệu/count bằng query/projection EF Core rồi dựng response trong bộ nhớ; nếu chưa batch được thì await tuần tự. Không cần thêm thư viện. [Tài liệu EF Core](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/#avoiding-dbcontext-threading-issues).

### Cursor Events không được áp dụng

Trong [EventsService.cs](../backend/Fookbase.Src/Code/Modules/Events/Services/EventsService.cs):

- `ListAsync`, dòng 401–406, nhận `cursor` và `desc` nhưng không dùng chúng để lọc/sắp xếp query. Các danh sách mine/upcoming/discover đi qua hàm này.
- `InvitationsAsync`, `ParticipantsAsync` và `PostsAsync`, dòng 250–282, cũng nhận cursor nhưng không áp dụng vào query.

Tải trang tiếp theo với cùng dữ liệu có thể trả lại trang đầu. Nên áp dụng keyset pagination như Groups/Notifications đang làm và bổ sung test qua ít nhất hai trang, gồm trường hợp timestamp trùng nhau. Đây là lỗi chức năng, không giải quyết bằng đổi thư viện.

### Cursor ảnh album dùng phần tử chưa trả về

[PhotosService.cs](../backend/Fookbase.Src/Code/Modules/Photos/Services/PhotosService.cs), dòng 78–81, trả `rows.Take(limit)` nhưng tạo cursor từ `rows[limit]`; trang tiếp theo lọc lớn hơn cursor. Với ba ảnh và `limit=1`, ảnh thứ hai có thể bị bỏ qua. Cursor cần dùng phần tử cuối đã trả về. Nên có test duyệt hết nhiều trang, không trùng và không thiếu ảnh.

### Regression Identity hết kết nối PostgreSQL

Lệnh chuẩn `bash scripts/test-backend.sh` dừng ở Identity: **216 pass, 10 fail / 226**, với `Npgsql.PostgresException: 53300: sorry, too many clients already`. Các lỗi xuất hiện trong `AdminDashboardTests` khi factory mở kết nối/apply migration.

Chưa xác định đầy đủ nguồn giữ kết nối. Cần kiểm tra vòng đời các host/factory/data source và pool trong test. Test assembly đã tắt parallelization, nên không nên quy lỗi đơn thuần cho việc chạy test song song hoặc chỉ tăng `max_connections`. Không thay đổi cấu hình database production trong lượt rà soát này.

## 2. Code không dùng và phần còn sót

### Web: 17 file source, 336 dòng

Knip mặc định và lần chạy bổ sung entry cho các test/browser script cùng xác định các file sau không đi vào import graph của app/test:

| Nhóm | File dưới `frontend/web/src` | Số dòng |
| --- | --- | ---: |
| Component không dùng | `shared/components/LikeButton.tsx` | 164 |
| Hiệu ứng không dùng | `shared/components/effects/GlitchText.tsx`, `ParticleCanvas.tsx` | 140 |
| File chỉ re-export không được import | `layout/index.ts`, `pages/index.ts`, `shared/index.ts`, `shared/components/index.ts`; `pages/{auth,explore,feed,games,groups,pages,profile,reels,search,stories}/index.ts` | 32 |

Route hiện tại import trực tiếp các page, kể cả lazy import. Các file `index.ts` ở bảng trên không cần cho luồng này. Có thể bỏ các file đã xác nhận không dùng cùng hai dependency `styled-components` và `@types/styled-components`. `LikeButton` là nơi duy nhất import `styled-components`; dependency này không được dùng bởi app đang chạy.

Ngay cả nếu giữ lại LikeButton để dùng sau, `styled-components` v6 đã có type riêng, nên `@types/styled-components` v5 vẫn thừa. [Tài liệu styled-components](https://styled-components.com/docs/api#typescript).

Hai asset mẫu `frontend/web/src/assets/react.svg` và `vite.svg` cũng không có reference trong source/HTML đã kiểm tra. Có thể bỏ sau khi xác nhận không có tiêu thụ ngoài repository.

### Mobile: API albums chưa có nơi gọi

[frontend/mobile/src/api/albums.ts](../frontend/mobile/src/api/albums.ts) không được route/component/test import. Đây là API client chưa được nối vào giao diện. Nếu chưa làm chức năng albums trên mobile ở bản sắp tới, có thể bỏ và tạo lại từ hợp đồng API khi cần; nếu có kế hoạch triển khai thì giữ và ghi rõ kế hoạch. Không nên gọi mọi code chưa nối UI là logic nghiệp vụ vô dụng.

### Cấu hình và tên test Minio còn sót

Runtime đã dùng Cloudinary, nhưng sáu `*ApiFactory.cs` trong các nhóm Identity/Users/Friends/Messages/Media/Posts vẫn đặt `Minio:*`; `ProductionRuntimeTests` cũng giữ cấu hình Minio mẫu.

[HealthEndpointsTests.cs](../backend/Fookbase.Test/Code/Identity/HealthEndpointsTests.cs) còn `UnreadyMinioIdentityApiFactory` và test `Readiness_fails_when_required_minio_bucket_is_unavailable`, dù health check runtime là `cloudinary`. Đặt `Minio:Endpoint` không còn điều khiển dependency đang được kiểm tra. Nên bỏ setting hết tác dụng, đổi tên đúng và làm tình huống Cloudinary lỗi xác định được bằng cơ chế test/DI có sẵn.

### Tài liệu chưa khớp cấu trúc hiện tại

- README gốc liệt kê bốn client, bỏ sót `frontend/mobile`, trong khi thư mục này có app và CI riêng.
- README web còn nhiều đoạn scaffold React/Vite. Nên giữ thông tin chạy app, API và SEO; bỏ hướng dẫn mẫu không phản ánh implementation.
- CI đang cố ý chỉ chạy bằng `workflow_dispatch`. Khi chạy thủ công, job web mới chạy `test:group-header`, chưa chạy các test Node còn lại; job frontend không chạy `messageTime.test.mjs` của Zola Light. Nên bổ sung các bộ test vào workflow hiện có. Việc bật lại trigger tự động là lựa chọn vận hành riêng.

## 3. Logic lặp có thể tái sử dụng

### Hai app mobile

So sánh nội dung file tìm thấy **12 file runtime giống hệt nhau, 482 dòng lặp ở một bản sao**, cùng **8 file test giống hệt, 357 dòng**:

| Phần | File runtime giống nhau dưới `src` |
| --- | --- |
| API | `api/client.ts`, `api/media.ts`, `api/messages.ts` |
| Auth/cấu hình | `auth/AuthProvider.tsx`, `config/env.ts` |
| Realtime | `realtime/RealtimeProvider.tsx` |
| Media/draft | `components/PickedMediaPreview.tsx`, `features/drafts/sessionDraft.ts`, `features/drafts/useDraftLeave.ts` |
| Hội thoại | `app/conversations/[conversationId].tsx`, `create.tsx`, `members.tsx` |

Cân nhắc một package nội bộ dùng chung cho API/auth/realtime và component hội thoại, giữ entry route/config thương hiệu riêng cho từng app. Đây là thay đổi cấu trúc cần kiểm tra Metro, TypeScript, Jest và Expo Router; nên làm từng phần sau bước dọn code không dùng. 482 dòng là phần trùng hiện tại, không phải cam kết số dòng giảm ròng sau khi thêm cấu hình tích hợp. Giữ test đặc thù của từng app.

### API client và DTO TypeScript

Các file API ngoài `client.ts` và test có tổng cộng khoảng **3.841 dòng** ở web/mobile/zola-mobile/admin. Con số gồm type và wrapper endpoint, không phải tất cả đều trùng. Riêng `friends.ts`, `reels.ts`, `privacy.ts`, `stories.ts`, `ai.ts` đang giống hệt giữa web và mobile.

Backend đã có Swagger. Có thể dùng Orval để sinh type và endpoint/hook từ OpenAPI, giữ transport hiện tại làm custom mutator. Cần hoàn thiện response schema trước: nhiều controller trả `IResult` và chưa khai báo `ProducesResponseType`; Identity dùng response envelope khác các module còn lại. Generator cần hiểu đúng hợp đồng này, cookie web, token mobile và refresh flow. Code sinh tự động vẫn tồn tại; lợi ích là giảm code phải tự viết và đồng bộ thủ công. [Orval React Query](https://orval.dev/docs/guides/react-query/).

### Backend

- Group/Notification/Message/Search/Reels/Stories lặp việc đổi ký tự/padding Base64URL. Dùng trực tiếp `WebEncoders.Base64UrlEncode/Decode` của ASP.NET Core để bỏ phần xử lý thủ công. Giữ payload, validation và format cursor cũ; Photos/Events đang dùng Base64 thường, không tự ý đổi format. Các cursor được Data Protection bảo vệ cũng phải giữ protection và scope. [Encode](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.webutilities.webencoders.base64urlencode?view=aspnetcore-10.0), [Decode](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.webutilities.webencoders.base64urldecode?view=aspnetcore-10.0).
- Nhiều controller lặp lấy claim `sub` và parse Guid, trong khi đã có `IdentityHttpHelpers.GetUserId()`. Tái sử dụng cho endpoint bắt buộc đăng nhập khi status/error contract tương thích. Endpoint cho phép anonymous cần giữ logic nullable; không ép tất cả dùng cùng helper rồi vô tình đổi 401/response.
- `PagedResponse<T>` của Identity/Users/Friends/Posts cùng shape; Messages có thêm `NextCursor`. Phần trùng rất nhỏ, chưa đáng thêm abstraction hoặc thư viện chỉ để gom vài record. Nếu thống nhất hợp đồng pagination trong một yêu cầu riêng thì mới hợp nhất DTO phù hợp.

## 4. Thư viện/công cụ đáng cân nhắc

| Công cụ | Hiện trạng | Phần có thể tối giản | Ưu tiên |
| --- | --- | --- | --- |
| EF Core query/projection | Đã có | Batch count/host/profile cho Photos/Events, bỏ query trong từng mapper | Cao, đồng thời xử lý lỗi |
| WebEncoders | Có trong ASP.NET Core | Base64URL thủ công | Cao, thay đổi nhỏ |
| TanStack Query | Đã có trong hai mobile; web chưa có | Loading/error/cache/cursor/cancel/refetch ở Feed, Saved, Search, Profile | Cao nếu tiếp tục phát triển web |
| Knip | Đã chạy qua npm exec, chưa thêm dependency | Theo dõi file/export/dependency không dùng | Cao, dùng cho kiểm tra |
| Orval | Chưa có | Type/endpoint/hook API viết tay trên nhiều client | Sau khi OpenAPI có schema đầy đủ |
| Radix Dialog | Chưa có | Focus trap, Escape, portal, khôi phục focus và quản lý modal | Cân nhắc khi sửa hệ thống dialog |
| Lucide React | Admin đã có; web chưa có | Các icon SVG thông dụng đang tự viết | Vừa; giữ icon thương hiệu/game |

[TanStack Query](https://tanstack.com/query/latest/docs/framework/react/guides/infinite-queries) cung cấp `useInfiniteQuery` với page/cursor và trạng thái tải tiếp. `FeedPage`, `SavedPostsPage`, `SearchPage` đang tự quản lý nhiều state/ref tương ứng. Nên thử trên Saved/Search trước, rồi mới chuyển Feed/Profile; giữ snapshot/scroll restoration, phân biệt user trong query key, clear cache khi logout và nối AbortSignal vào API client. SignalR vẫn dùng SDK hiện tại; Query hỗ trợ quản lý dữ liệu và invalidate/update cache, không thay thế kết nối realtime.

[Radix Dialog](https://www.radix-ui.com/primitives/docs/components/dialog) có sẵn xử lý focus và bàn phím. Hiện `useDialogFocus.ts` tự quản lý stack, khóa scroll và vòng Tab; cân nhắc thay khi cần mở rộng modal, không cần kéo cả bộ UI framework vào chỉ cho một dialog.

Web có 66 phần tử SVG inline; [Lucide React](https://lucide.dev/guide/react) có thể thay những icon phổ thông và cho phép import riêng icon cần dùng. Không nên mặc định thay toàn bộ hình vẽ riêng của dự án.

Không cần thêm Axios chỉ để thay `fetch`, AutoMapper cho các mapper có query/phân quyền, MediatR/repository wrapper, FluentValidation cho vài annotation đơn giản, hoặc thư viện số điện thoại cho bộ quy tắc Việt Nam ngắn hiện tại. Lợi ích phải lớn hơn phần cấu hình và dependency phát sinh.

## 5. Những phần nên giữ

- `MemoryTodayResponse` và `MemoryYearResponse` hiện là record ngắn, diễn đạt hai cấp dữ liệu của API; không phải code rác và không cần thư viện thay thế.
- Có khoảng 126.530 dòng migration designer/snapshot trong 49 file. Đây là phần EF sinh và lịch sử database, không phải 126 nghìn dòng cần xóa. Không squash/xóa migration của database đã triển khai trong đợt dọn code.
- Các service lớn như Posts/Friends/Groups chứa nghiệp vụ, phân quyền và batch query. Có thể tách theo trách nhiệm khi có nhu cầu cụ thể, nhưng độ dài file không chứng minh code vô dụng; bỏ validation/privacy/transaction để ngắn hơn sẽ đổi hành vi.
- Các test/browser script chạy trực tiếp không phải file rác chỉ vì Knip mặc định không tìm thấy entry. Đã bổ sung entry ở lần kiểm tra web để loại cảnh báo này.
- Knip báo `@expo/ui`, `expo-glass-effect`, `expo-splash-screen`, `expo-status-bar` và một số dependency mobile chưa được import trực tiếp. `npm ls` cho thấy Expo Router dùng `@expo/ui`/`expo-glass-effect`; Expo còn có tích hợp native. Không tự động uninstall chỉ theo kết quả scan. Cảnh báo `expo-updates` từ plugin Expo cũng cần đối chiếu config thực tế trước khi kết luận thiếu dependency.
- `tailwindcss` được import qua CSS. Lần cấu hình Knip chỉ include TS/TSX sinh cảnh báo thừa giả; không bỏ dependency này.

## 6. Kết quả kiểm tra

| Phần | Kiểm tra | Kết quả |
| --- | --- | --- |
| Backend | Build solution bằng SDK Docker cùng NuGet cache project | Pass, 0 warning, 0 error |
| Backend Identity | Script integration chuẩn, PostgreSQL cô lập | 216/226 pass; 10 fail vì hết kết nối |
| Backend các nhóm còn lại | Chạy tuần tự bằng bản script tạm giữ cơ chế DB cô lập, bỏ nhóm Identity đã chạy | Pass cả 5 nhóm: Users 47, Friends 57, Messages 120, Media 53, Posts 528; tổng 805 test |
| Web | Lint, production build, `test:games`, `test:group-header`, `test:auth` | Pass; lần lượt 160 + 1 + 36 test |
| Admin | Lint, production build | Pass |
| Zola Light | Lint, production build, `node --test tests/messageTime.test.mjs` | Pass; 3 test |
| Mobile | Typecheck, lint, Jest tuần tự | Pass; 93 test |
| Zola Mobile | Typecheck, lint, Jest tuần tự | Pass; 50 test |
| Hai mobile | Expo export Android | Pass cả hai app |
| Cả 5 client | Knip 6.40.0 và đối chiếu source | Hoàn tất; kết quả phân loại ở trên |

Build .NET trực tiếp với `--no-restore` ban đầu lỗi `NETSDK1064` do cache/restore path không khớp giữa host và Docker. Build với Docker theo convention repository thành công. Hai lượt Jest ban đầu có một timeout mỗi app khi chạy cùng đợt kiểm tra; chạy lại từng app với timeout mặc định đều pass, không sửa test để che lỗi. Nên theo dõi độ ổn định của hai suite này nếu CI cũng gặp timeout.

Chưa chạy các browser smoke cần API/Playwright và chưa build APK/iOS native. Expo export chỉ kiểm tra bundling Android, không thay cho native build.

## 7. Thứ tự triển khai đề xuất

1. Sửa query song song và cursor Photos/Events; thêm regression qua nhiều phần tử/trang. Điều tra lỗi connection của test Identity.
2. Bỏ 17 file web không dùng và dependency styled-components; dọn setting/tên test Minio, asset và tài liệu mẫu đã hết tác dụng. Commit riêng từng nhóm liên quan.
3. Thiết lập Knip với entry đúng và đưa các test hiện có vào CI thủ công. Không dùng autofix cho Expo hoặc browser script chưa phân loại.
4. Thử TanStack Query trên Saved/Search để đo code được giảm và xác nhận hành vi, rồi mới mở rộng.
5. Hoàn thiện response schema OpenAPI, thử generator trên một module; cân nhắc package mobile dùng chung sau khi thử nghiệm import/build/test.

Báo cáo được đưa lên nhánh tài liệu riêng vì push `main` hiện tự chạy workflow deploy EC2. Mọi thay đổi runtime đề xuất ở trên là công việc tiếp theo, chưa được áp dụng trong lượt rà soát.
