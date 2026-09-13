# Task 5 — Feed theo dõi-aware

## TDD

- Bổ sung test trước phần triển khai cho: bạn bè đã theo dõi, bỏ theo dõi ở phiên mới,
  người không phải bạn đã theo dõi (Public và Friends), Reel organic, kiểm tra lại cursor
  cũ, Following dùng cạnh Follow và không chèn Reel gợi ý.
- Bổ sung regression review: unfriend giữ cạnh Follow nhưng phải thu hồi bài Friends-only
  ở Feed mới lẫn cursor cũ; unfollow bạn bè loại Reel Public/Friends khỏi nguồn organic
  (Reel Public chỉ còn có thể quay lại với cờ suggested); và thứ tự affinity của người được
  theo dõi nhưng không phải bạn bè nằm giữa friend và group/page.
- Lần chạy red trực tiếp bằng `dotnet test ... --filter "FullyQualifiedName~MixedFeed"`
  không khởi động được testhost vì máy host thiếu `Microsoft.AspNetCore.App 10.0.0`.
  Đây là thiếu runtime của môi trường, không phải lỗi test hay mã nguồn.

## Thay đổi

- Snapshot quan hệ nạp `FollowedUserIds` theo cạnh đi, loại trừ quan hệ bị block.
- Feed xây lại snapshot ở từng yêu cầu (kể cả cursor cũ), rồi tách nguồn profile thành:
  bản thân, bạn bè đã theo dõi (Public/Friends), và người không phải bạn đã theo dõi
  (Public).
- Thêm affinity `FollowedNonFriendProfile = 5`, nằm dưới friend (6) và trên group/page
  (4/3); option này được đưa vào ranking hash hiện có.
- Following vẫn có điểm 0, dùng nguồn Follow, và Reel của người đã theo dõi không bị lặp
  lại ở discovery; discovery chỉ lấy người chưa theo dõi. Nhóm, page và share không đổi.
- Fixture friendship của mixed-feed phản ánh invariant hiện có: chấp nhận kết bạn tạo
  follow hai chiều. Unfriend giữ follow, nhưng nguồn friend và quyền Friends-only vẫn bị
  thu hồi ngay ở Feed mới và khi đi tiếp bằng cursor cũ; unfollow được kiểm tra riêng.

## Kiểm tra

- `dotnet build backend/Fookbase.Src/Main/Fookbase.Api.csproj --no-restore` — PASS.
- `dotnet build backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/Fookbase.Posts.Api.IntegrationTests.csproj --no-restore` — PASS.
- PostgreSQL tạm thời và SDK Docker cô lập:
  `dotnet vstest ...Fookbase.Posts.Api.IntegrationTests.dll --TestCaseFilter:'FullyQualifiedName~MixedFeed'`
  — PASS, 23/23 sau khi bổ sung regression review.
- Review bổ sung cũng chạy `dotnet build ...Fookbase.Posts.Api.IntegrationTests.csproj --no-restore`
  — PASS (0 warning, 0 error); `git diff --check` trên hai file Task 5 — PASS.
- `git diff --check` trên các file Task 5 — PASS.

## Tự rà soát / blocker

- Giữ nguyên `AsOfUtc`, Data Protection purpose theo ranking version, lookahead và keyset
  cursor; không đổi group/page/share eligibility.
- Không còn blocker chức năng. Runtime ASP.NET Core thiếu trên host đã được khắc phục cho
  kiểm tra bằng container SDK tạm thời, sau đó container và database tạm đã được xóa.
