# Tần số 0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

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

- [ ] Write behavioral tests for camera-relative movement, normalized diagonals, running, long frames, wall/rock/ceiling collision, landing, unlocked door, visible/ranged interaction, ordered objectives, paused progress and single-fire room event.
- [ ] Run `node --test tests/lastSignal.test.mjs` and confirm new behavior fails before implementation.
- [ ] Implement the shared world data and minimal tested controller/state/interaction functions.
- [ ] Run `npm run test:games`, `npm run lint`, `npx tsc -b` and inspect the diff.
- [ ] Commit `feat: thêm gameplay và va chạm 3D cho Tần số 0` and push.

### Task 2: WebGL world, player and route integration

**Files:** Add dependencies; create `frontend/web/src/game/{Game.tsx,GameScene.tsx,game.css,hooks/useKeyboard.ts,player/Player.tsx,world/World.tsx,world/Environment.tsx,gameplay/audio.ts,ui/GameHUD.tsx,ui/GameMenus.tsx}`; modify `frontend/web/src/routes/index.tsx` and `frontend/web/src/pages/games/GamesPage.tsx`.

**Interfaces:** Consume Task 1 functions and world descriptors. `Game` owns UI state/runtime refs; scene uses `useFrame` without per-frame React transforms. Menus invoke start/resume/replay/mute/return; player exposes no test-only production hooks.

- [ ] Install Three/Fiber/Drei and Three types with npm, preserving package-lock.
- [ ] Build primitive forest/station meshes from the same data as colliders, lights/fog/shadows, perspective camera and body/flashlight meshes.
- [ ] Wire real keyboard and pointer-lock input, interaction, objective trigger, ambient/event audio, pause/visibility cleanup and replay.
- [ ] Build start/HUD/pause/completion/error overlays and responsive local CSS.
- [ ] Lazy-load `/game` under MainLayout; link it from the game library.
- [ ] Run Node tests, oxlint, `npx tsc -b`, `npm run build`; verify WebGL/input and loop in Chromium before commit.
- [ ] Commit `feat: tích hợp game WebGL Tần số 0 vào website` and push.

### Task 3: Browser regression and delivery

**Files:** Create a focused browser regression in `frontend/web/tests/browser/lastSignal.mjs` and usage/verification notes in `docs/last-signal.md`. Browser automation uses an installed Playwright package outside production dependencies, supplied by environment when running.

- [ ] Automate actual user inputs and game loop; inspect Fiber scene from browser test only to assert perspective/WebGL/meshes and observe actual player camera positions.
- [ ] Verify movement, wall collision, key, door, generator, single-fire trigger, receiver completion, pause/resume and replay.
- [ ] Verify resize/touch warning, navigating away/reentering, game library/login and absence of serious game console errors.
- [ ] Run all web tests/lint/typecheck/build and browser regression. Save screenshots/report in ignored local artifacts.
- [ ] Request one fresh code review of the full feature; fix important findings with regression coverage and rerun affected checks.
- [ ] Document commands/route/controls and verification results, commit `test: kiểm tra luồng chơi và hướng dẫn Tần số 0`, push and report commits.
