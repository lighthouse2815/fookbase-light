# Tần số 0 — playable WebGL prototype

## Intent and repository fit

Build a complete first-person exploration loop inside the existing Fookbase website. The player must move through an actual perspective 3D scene, collide with objects, interact, unlock a room and resolve its mystery. Desktop is the primary target; narrow/touch screens must remain usable as a website and display “Desktop controls recommended”.

The web client is `frontend/web`: React 19, TypeScript 6, Vite 8, npm/package-lock, React Router 7, Tailwind 4 and feature-local CSS. Existing games use Node's built-in test runner. `/game` is lazy-loaded within the existing authenticated MainLayout. `/games` gains a featured link; existing games, authentication, backend and homepage are unchanged.

## Technical choices

Use Three.js, @react-three/fiber and @react-three/drei. Fiber owns WebGL rendering, perspective camera, resize and declarative mesh disposal; Drei supplies pointer-lock controls. Compared with imperative Three.js this matches React's lifecycle; Babylon would add a second set of scene conventions with no benefit here.

Use a player AABB with full X/Y/Z overlap against world colliders, gravity and fixed substeps. Reuse the same box definitions for rendering and collision. This is sufficient for a small static map with a dynamic door, without Rapier's additional runtime. Movement follows camera yaw, normalizes diagonals, uses delta time, and substeps avoid tunneling. Jump supports landing on low props and ceiling collision.

## World and presentation

Title: **TẦN SỐ 0 / THE LAST SIGNAL**. A foggy forest surrounds an abandoned transmission station. Primitive meshes form ground, paths, enclosing walls, the station's roof and walls, a tool shelter, rocks, trunks, equipment, crates and an antenna. Cool moonlight and warm lamps establish depth; the player's 3D flashlight illuminates dark areas. MeshStandardMaterial roughness/metalness provide surface variation, without external model or texture downloads.

The first-person player is a 3D group with a body and flashlight/hand meshes. Camera follows eye height with subtle movement smoothing; PointerLockControls handles mouse look with bounded pitch. Directional light casts 1024px shadows, hemisphere light supplies visibility, and limited local point/spot lights establish contrast. No sprites or CSS transforms substitute for scene geometry.

## Gameplay contract

1. Spawn at the entrance facing the station. Start Game explicitly captures the pointer.
2. Follow the warm shelter light to collect the station key with E. A note hints at the generator.
3. Unlock the station's front door with E. The open door stops blocking the entrance.
4. Restore power at the generator inside the station with E.
5. Enter the newly lit back room: the door closes, lights flicker briefly and a distant 3D silhouette appears. This trigger runs once.
6. Inspect the receiver with E to finish. Completion releases the pointer and offers replay or return to the library.

Interactions use descriptors with IDs, 3D positions, enabled conditions and action kinds. Find the closest visible object within 2.5m using distance, view alignment and occlusion against colliders; E repeats do not fire repeatedly. HUD objective, inventory and messages update only at meaningful state changes. An optional lamp and note demonstrate extensibility.

## Runtime and UI

Feature-local modules separate world data/rendering, player controller, interaction selection, objective state, trigger, audio and HTML overlay. Runtime transforms and elapsed time live in refs; React state contains only phase, objectives, prompt and discrete events. Development diagnostics refresh at most four times per second and show position/FPS/draw calls.

Start overlay includes story, controls and Start Game. HUD includes objective, crosshair, inventory, interaction prompt and pause button. ESC, losing pointer lock, blur or hidden tab pause movement and audio. Resume requires another explicit click. Pointer-lock/WebGL errors show actionable overlays. Replay resets player, door, key, generator and trigger. Audio uses the existing browser Web Audio API, starts only after a user gesture and disposes on unmount; mute is available. No external asset requests are required.

Canvas fits the current layout/container, capped device pixel ratio, no unwanted horizontal overflow. Start/pause/completion use an idle render loop; gameplay animates continuously. Listener/resource/audio cleanup permits navigation away and repeated entry.

## Verification

Node behavioral tests cover yaw-relative and diagonal movement, running, delta-time independence, wall/rock/ceiling collisions, gravity/jump, door state, interaction distance/occlusion, ordered objective progression, single-fire trigger, paused state and reset.

Run all existing game/auth and group-header tests, oxlint, TypeScript and production build. Browser verification with real Chromium/WebGL checks loading, actual perspective scene, pointer lock, WASD/mouse input, collision, E, complete loop, pause/resume, replay, resize, touch warning, console errors and navigation to existing routes. Browser tests use an isolated synthetic session and API fixtures; no user credentials or database changes.

## Delivery

Work directly in the clean current checkout as requested. Commit independent meaningful parts using short Vietnamese Conventional Commit messages and push to existing GitHub origin. No force push or history rewriting.
