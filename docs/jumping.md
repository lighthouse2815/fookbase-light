# Jumping

Mở `/games`, chọn **Jumping**. Nhân vật tự chạy; nhấn Space, mũi tên lên, W hoặc chạm đường chạy/nút Nhảy để vượt chướng ngại vật. Mỗi khối vượt qua được một điểm. Phải chạm đất trước khi nhảy tiếp; tốc độ tăng dần. Kỷ lục lưu trên trình duyệt.

Chơi đơn có nút tạm dừng/tiếp tục và phím P hoặc Escape. Chuyển tab tự tạm dừng.

## Chơi cùng bạn bè

Đăng nhập Fookbase, tạo phòng và chia sẻ mã sáu ký tự. Bạn bè nhập mã để vào; chủ phòng bắt đầu vòng sau ba giây đếm ngược. Mọi người có cùng seed, thời điểm bắt đầu và đường chạy, nhưng điều khiển nhân vật riêng. Bảng điểm cập nhật trực tiếp. Người vào giữa vòng xem điểm và chơi từ vòng kế tiếp.

Giữ tab mở đến hết vòng online; chuyển tab kết thúc lượt, kể cả trong lúc đếm ngược. Chủ phòng có thể mở vòng mới khi các người chơi còn lại đã kết thúc. Khi chủ phòng rời, quyền chuyển sang một người còn trong phòng. Mất mạng tự thử kết nối lại; nếu phòng đã đóng, tạo hoặc vào phòng lại.

## Implementation

- `frontend/web/src/pages/games/jumpingEngine.ts`: bước vật lý cố định, va chạm chung tọa độ với SVG, sinh chướng ngại vật bằng seed.
- `JumpingGame.tsx`, `jumping.css`: màn chơi responsive, bàn phím/chạm, âm thanh dùng hook hiện có.
- `useJumpingRoom.ts`: vòng đời SignalR `/hubs/jumping`, phòng, đếm ngược và điểm người chơi.
- Module Games dùng `GameRoomService` và `GameRoomHub` chung cho Jumping/Flappy Bird. Hai game có room store riêng bằng keyed DI; không thay schema hoặc thêm dependency.

Phòng tồn tại trong một process API; restart API đóng phòng. Điểm do client báo và được hub kiểm tra định dạng/phạm vi, phục vụ chơi cùng bạn bè, không phải bảng xếp hạng có chống gian lận.

## Kiểm tra

```bash
cd frontend/web
npm run test:games
npm run lint
npm run build
```

Từ root repository:

```bash
dotnet test backend/Fookbase.Test/Fookbase.Test.csproj --no-restore --filter FullyQualifiedName~Fookbase.Games.Tests
```

Browser smoke dùng Playwright đã cài, theo convention các script trong `tests/browser`. Chạy Vite dev/preview trước, đặt `GAME_BASE_URL` đến URL đó và `PLAYWRIGHT_MODULE` đến `index.mjs` của bản Playwright đang có, rồi chạy:

```bash
node tests/browser/jumping.mjs
```

Script kiểm tra bàn phím/chạm, nhảy, điểm/kỷ lục, pause/resume và chơi lại trên desktop/mobile. Để kiểm tra online với hub thật, trỏ proxy `/hubs` của preview tới API và cung cấp `JUMPING_HOST_ID`, `JUMPING_GUEST_ID`, `JUMPING_HOST_TOKEN`, `JUMPING_GUEST_TOKEN` của hai tài khoản test qua environment. Không lưu token vào Git. Khi có hai token, script kiểm tra thêm tạo/vào phòng, đường chạy chung, điểm trực tiếp, vào giữa vòng, tự vào lại phòng sau khi ngắt WebSocket, chuyển chủ phòng và chuyển tab lúc đếm ngược.
