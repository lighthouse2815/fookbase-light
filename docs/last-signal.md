# Tần số 0 / The Last Signal

Game khám phá 3D góc nhìn thứ nhất tại **`/game`**, tích hợp trong website Fookbase hiện tại. Vào mục **Chơi game** và chọn thẻ **Tần số 0**, hoặc truy cập route trực tiếp sau khi đăng nhập.

## Chạy website

```bash
cd frontend/web
npm install
npm run dev
```

Website chạy tại `http://localhost:5173`; game tại `http://localhost:5173/game`. Dùng backend/config đăng nhập hiện có của project. Game không cần API gameplay, model hoặc file âm thanh từ bên ngoài.

Nhấn **Start Game** để giữ chuột. Nếu trình duyệt từ chối, thông báo lỗi cho phép bấm lại; chỉ nút Start/Tiếp tục mới yêu cầu giữ chuột. Nếu WebGL không hoạt động, bật tăng tốc phần cứng và dùng nút tải lại.

## Điều khiển và mục tiêu

| Phím | Hành động |
| --- | --- |
| WASD | Di chuyển theo hướng nhìn |
| Chuột | Xoay camera |
| Shift | Chạy |
| Space | Nhảy |
| E | Tương tác khi thấy prompt |
| ESC | Tạm dừng và thả chuột |

Nút âm thanh trong menu cho phép tắt/bật âm thanh nền. Rời tab hoặc mất focus sẽ tạm dừng. Tiến trình nằm trong lượt chơi; chơi lại hoặc rời route sẽ bắt đầu một lượt mới.

Luồng chơi: tìm chìa khóa trên bàn trong lán có đèn vàng bên trái → mở cửa nhà trạm 07 → khởi động máy phát bên trái trong trạm → đi qua cửa hẹp đến phòng sau → điều tra máy thu. Phòng cuối kích hoạt đèn chớp, cửa đóng và một bóng người 3D xuất hiện ngắn. Có thể đọc ghi chú và bật/tắt đèn lán bằng E.

Desktop là nền tảng gameplay chính. Mobile vẫn render trang và hiển thị **Desktop controls recommended**; chưa có điều khiển cảm ứng.

## Cấu trúc

- `frontend/web/src/game/Game.tsx`: lifecycle, pause/reset, audio và các trạng thái UI.
- `GameScene.tsx`: Canvas WebGL, PerspectiveCamera, pointer lock và giảm DPR khi FPS thấp.
- `player/`: nhân vật/đèn pin 3D, chuyển động theo camera, gravity, substep và va chạm AABB X/Y/Z.
- `world/`: ground texture tạo bằng DataTexture, cây instanced, nhà trạm, props, đèn, shadow và dữ liệu collider.
- `interaction/`: tìm vật thể trong phạm vi/hướng nhìn và chặn tương tác xuyên tường.
- `gameplay/`: chuỗi mục tiêu, trigger một lần, âm thanh Web Audio.
- `ui/`, `hooks/`: HUD HTML/CSS và input có cleanup.

Thêm một interaction bằng descriptor trong `world/worldData.ts`, bổ sung ID/action trong `types.ts` và `gameplay/ObjectiveSystem.ts`, rồi thêm mesh trong World. Geometry của vật cản chính dùng cùng dữ liệu với collider. Cánh cửa đang mở vẫn có collider tại vị trí cánh cửa.

Engine tải riêng theo route; Vite báo chunk game khoảng 956KB, khoảng 256KB gzip. DPR ban đầu tối đa 1.5, giảm xuống 0.75 khi cần; shadow 1024/512px, cây instanced, biến đổi mỗi frame dùng refs. Fiber dispose scene/renderer khi unmount, texture tự tạo và AudioContext có cleanup riêng. Các bộ đếm texture nội bộ có thể còn giá trị sau dispose; browser regression xác minh geometry đã dispose, native WebGL context đã được giải phóng và audio đã đóng. Xem [hướng dẫn dispose của Three.js](https://threejs.org/manual/pages/how-to-dispose-of-objects.html).

## Kiểm thử

Từ `frontend/web`:

```bash
npm run test:games
npm run test:auth
npm run test:group-header
npm run lint
npx tsc -b
npm run build
```

`tests/lastSignal.test.mjs` có 14 test hành vi: chuyển động theo yaw, diagonal/run/delta time, collision/ceiling/jump/landing, cửa đóng/mở, pause, khoảng cách/hướng nhìn/occlusion, mục tiêu theo thứ tự, reset và trigger một lần.

Kết quả kiểm tra ngày 04/10/2026: 68 test game/shared, 18 test auth và 1 test group-header đều pass; lint, typecheck và build thành công. Browser regression hoàn tất 14 kiểm tra với scene WebGL2 có 77 mesh; smoke production xác nhận projection perspective của renderer. Cảnh báo kích thước chunk của Vite nêu ở trên không chặn build.

Browser regression dùng Playwright cài ngoài dependencies của website. Các API và session trong browser test là fixture riêng, không dùng credential hay sửa database thật:

```bash
npm install --prefix /tmp/last-signal-browser playwright
/tmp/last-signal-browser/node_modules/.bin/playwright install chromium
```

Chạy dev server ở terminal thứ nhất:

```bash
npm run dev -- --host 127.0.0.1 --port 5183 --strictPort
```

Ở terminal thứ hai:

```bash
PLAYWRIGHT_MODULE=/tmp/last-signal-browser/node_modules/playwright/index.mjs node tests/browser/lastSignal.mjs
```

Để kiểm tra bundle production, chạy `npm run preview -- --host 127.0.0.1 --port 5184 --strictPort`, rồi:

```bash
PLAYWRIGHT_MODULE=/tmp/last-signal-browser/node_modules/playwright/index.mjs node tests/browser/lastSignalSmoke.mjs
```

Có thể đặt `BROWSER_EXECUTABLE` để dùng Chromium sẵn có, `GAME_BASE_URL` để đổi server và `GAME_ARTIFACTS` để đổi thư mục screenshot/report (mặc định `/tmp/last-signal-artifacts`). Full regression dùng dev server để quan sát scene qua Fiber; smoke production xác minh projection perspective trực tiếp từ uniform của WebGL, không cần hook trong code game.

Đã kiểm tra trên Chromium với WebGL2: toàn bộ gameplay loop, chuột/WASD/E/Space, collision, pause/resume/replay, note/đèn, trigger/hoàn tất, resize/mobile, rời route/vào lại, audio/resource cleanup, lỗi pointer lock/WebGL và authentication guard. Có snapshot Start, phòng cuối, completion, mobile và production. Các kiểm tra browser dùng SwiftShader; FPS của renderer phần mềm không được dùng để kết luận FPS trên GPU thật.
