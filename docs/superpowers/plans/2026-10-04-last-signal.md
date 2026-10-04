# Tần số 0 Implementation Plan

> **Execution:** Implement task-by-task in the current checkout, following the user's explicit request for direct, autonomous work. Preserve independent Vietnamese commits and push each completed deliverable.

**Goal:** Integrate a complete playable first-person 3D exploration game at `/game`.

**Architecture:** Feature-local Fiber scene with shared world/collider data, mutable player runtime and discrete React HUD state. Drei pointer lock controls camera rotation; a substepped AABB player controller handles 3D movement and gravity. No backend or existing game behavior changes.

**Tech Stack:** Existing React/TypeScript/Vite/npm, Three.js, React Three Fiber/Drei, Node test runner, browser Web Audio.

**Spec:** `docs/superpowers/specs/2026-10-04-last-signal-design.md`

## Global Constraints

- Work in `frontend/web` and preserve existing layout/authentication/routes.
- Actual WebGL renderer, PerspectiveCamera, X/Y/Z meshes, materials, lights, shadows and 3D collision.
- Desktop WASD/mouse/Shift/Space/E/ESC; touch displays “Desktop controls recommended”.
- No external model/texture/audio downloads; no extra physics or application state dependency.
- Per-frame transforms use refs; reset, pause, unmount and errors must cleanly stop gameplay.
- Commit each meaningful independent deliverable and push existing GitHub origin; no force push.

## Review Focus

- Large frame gaps and fast diagonal movement must not tunnel through walls or the locked door.
- Interactions near a wall must not collect items or activate switches through the wall.
- Pointer-lock rejection, ESC, blur and hidden tabs must leave movement paused with a working resume button.
- Replay and navigation must reset the trigger and remove global input/audio resources.
- Small viewports/WebGL unavailability must show usable UI without breaking the website.

### Task 1: Tested gameplay and 3D collision core

**Files:** Create `frontend/web/src/game/{types.ts,world/worldData.ts,player/PlayerController.ts,interaction/InteractionSystem.ts,gameplay/ObjectiveSystem.ts,gameplay/TriggerSystem.ts}` and `frontend/web/tests/lastSignal.test.mjs`.

**Interfaces:** `createPlayer(): PlayerState`, `stepPlayer(player, input, yaw, dt, colliders): void`; `createProgress(): GameProgress`, `interact(progress, id): GameProgress`; `findInteraction(player, direction, progress, colliders): InteractionId | null`; `enterStrangeRoom(progress, position): GameProgress`. World exports static boxes/colliders plus key/door/generator/receiver locations.

- [x] Write behavioral tests for camera-relative movement, normalized diagonals, running, long frames, wall/rock/ceiling collision, landing, unlocked door, visible/ranged interaction, ordered objectives, paused progress and single-fire room event.
- [x] Run `node --test tests/lastSignal.test.mjs` and confirm new behavior fails before implementation.
- [x] Implement the shared world data and minimal tested controller/state/interaction functions.
- [x] Run `npm run test:games`, `npm run lint`, `npx tsc -b` and inspect the diff.
- [x] Commit `feat: thêm gameplay và va chạm 3D cho Tần số 0` and push (`c1ac456`; parked-door collision regression/fix `026b81a`).

### Task 2: WebGL world, player and route integration

**Files:** Add dependencies; create `frontend/web/src/game/{Game.tsx,GameScene.tsx,game.css,hooks/useKeyboard.ts,player/Player.tsx,world/World.tsx,world/Environment.tsx,gameplay/audio.ts,ui/GameHUD.tsx,ui/GameMenus.tsx}`; modify `frontend/web/src/routes/index.tsx` and `frontend/web/src/pages/games/GamesPage.tsx`.

**Interfaces:** Consume Task 1 functions and world descriptors. `Game` owns UI state/runtime refs; scene uses `useFrame` without per-frame React transforms. Menus invoke start/resume/replay/mute/return; player exposes no test-only production hooks.

- [x] Install Three/Fiber/Drei and Three types with npm, preserving package-lock.
- [x] Build primitive forest/station meshes from the same data as colliders, lights/fog/shadows, perspective camera and body/flashlight meshes.
- [x] Wire real keyboard and pointer-lock input, interaction, objective trigger, ambient/event audio, pause/visibility cleanup and replay.
- [x] Build start/HUD/pause/completion/error overlays and responsive local CSS.
- [x] Lazy-load `/game` under MainLayout; link it from the game library.
- [x] Run Node tests, oxlint, `npx tsc -b`, `npm run build`; verify WebGL/input and loop in Chromium before commit.
- [x] Commit `feat: tích hợp game WebGL Tần số 0 vào website` and push (`969d1fe`).

### Task 3: Browser regression and delivery

**Files:** Create a focused browser regression in `frontend/web/tests/browser/lastSignal.mjs`, production smoke in `lastSignalSmoke.mjs`, shared site fixtures in `lastSignalFixture.mjs`, and usage/verification notes in `docs/last-signal.md`. Browser automation uses an installed Playwright package outside production dependencies, supplied by environment when running.

- [x] Automate actual user inputs and game loop; inspect Fiber scene from browser test only to assert perspective/WebGL/meshes and observe actual player camera positions.
- [x] Verify movement, wall collision, key, door, generator, single-fire trigger, receiver completion, pause/resume and replay.
- [x] Verify resize/touch warning, navigating away/reentering, game library/login and absence of serious game console errors.
- [x] Run all web tests/lint/typecheck/build and browser regression. Save screenshots/report in ignored local artifacts.
- [x] Perform a separate code review of the full feature and use browser regression evidence; fix important findings and rerun affected checks.
- [x] Document commands/route/controls and verification results, commit `test: kiểm tra luồng chơi và hướng dẫn Tần số 0`, push and report commits.

**Verification:** 68 game/shared tests (including 14 Last Signal tests), 18 auth tests and 1 group-header test pass; lint, TypeScript and production build pass. Chromium regression completes all 14 checks with a real WebGL2/PerspectiveCamera scene containing 77 meshes. Production smoke validates the native GPU projection uniform, user input, pause and library navigation. Review confirms cleanup of input listeners, scene geometry/native WebGL context and Web Audio. The intentionally injected pointer-lock failure asserts its exact library error before retry; unexpected console/page errors still fail the regression. The lazy-loaded engine chunk is 956.40KB / 255.71KB gzip, with a non-blocking Vite size warning documented in the guide.
