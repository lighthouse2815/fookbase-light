# M11 runtime validation report

Date: 2026-09-13 (Asia/Ho_Chi_Minh)

## Scope and isolation

The smoke run used only the disposable Docker Compose project
`fookbase-m11-smoke`. It registered one disposable `@example.test` account,
created two disposable public posts, and saved both. The access token and
opaque protected cursor existed only in shell variables. They are deliberately
not included here, nor are any Data Protection key files or environment values.

An existing local Compose stack already occupied the ports exposed by
`compose.yml` (including `9001`, `5432`, and the configured API port). The
documented command was run first exactly as requested:

```bash
docker compose -p fookbase-m11-smoke up -d --build
```

It built the API image and created only project-scoped resources, then failed
before the API became ready with this exact Docker error:

```text
Error response from daemon: failed to set up container networking: driver failed programming external connectivity on endpoint fookbase-m11-smoke-minio-1 (...): Bind for 0.0.0.0:9001 failed: port is already allocated
```

The partial project was removed with `docker compose -p fookbase-m11-smoke
down -v --remove-orphans`. To complete the required isolated smoke test
without stopping or touching that pre-existing stack, a temporary, untracked
override at `/tmp/fookbase-m11-smoke.ports.yml` used Compose `!override` for
host ports only: API `15000:5000`, PostgreSQL `15432:5432`, MinIO console
`19001:9001`, and MinIO CORS `19000:9000`. Service names, network, volumes,
images, and all application configuration remained those from `compose.yml`.

```bash
docker compose -p fookbase-m11-smoke \
  -f compose.yml -f /tmp/fookbase-m11-smoke.ports.yml config --quiet
docker compose -p fookbase-m11-smoke \
  -f compose.yml -f /tmp/fookbase-m11-smoke.ports.yml up -d --build
until curl -fsS http://localhost:15000/health/ready >/dev/null; do sleep 2; done
docker compose -p fookbase-m11-smoke \
  -f compose.yml -f /tmp/fookbase-m11-smoke.ports.yml ps
docker volume inspect fookbase-m11-smoke_data-protection-keys \
  --format '{{.Name}} {{.Driver}} {{.Mountpoint}}'
```

Results: readiness succeeded; `postgres` reported `healthy`; API, MinIO, and
MinIO CORS were up; and the named volume
`fookbase-m11-smoke_data-protection-keys` existed with the local driver.
Docker also emitted a non-functional tooling warning that the buildx CLI plugin
was unavailable and the classic builder was used; the image build succeeded.

## Real protected-cursor recreation

The following shell operations were run, with the values kept only in that
shell session. `SMOKE_EMAIL`, `SMOKE_USERNAME`, `SMOKE_PASSWORD`,
`ACCESS_TOKEN`, post IDs, and `CURSOR` were never printed or written to a
file.

```bash
AUTH_RESPONSE=$(curl -fsS -X POST http://localhost:15000/api/auth/register \
  -H 'Content-Type: application/json' --data "$REGISTRATION_JSON")
ACCESS_TOKEN=$(printf '%s' "$AUTH_RESPONSE" | node -e '<read accessToken>')
POST_ONE=$(curl -fsS -X POST http://localhost:15000/api/posts \
  -H "Authorization: Bearer $ACCESS_TOKEN" -H 'Content-Type: application/json' \
  --data "$PUBLIC_POST_JSON")
POST_TWO=$(curl -fsS -X POST http://localhost:15000/api/posts \
  -H "Authorization: Bearer $ACCESS_TOKEN" -H 'Content-Type: application/json' \
  --data "$PUBLIC_POST_JSON")
curl -fsS -o /dev/null -X POST \
  "http://localhost:15000/api/posts/$POST_ONE_ID/save" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
curl -fsS -o /dev/null -X POST \
  "http://localhost:15000/api/posts/$POST_TWO_ID/save" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
FIRST_PAGE=$(curl -fsS -H "Authorization: Bearer $ACCESS_TOKEN" \
  'http://localhost:15000/api/posts/saved?limit=1')
CURSOR=$(printf '%s' "$FIRST_PAGE" | node -e '<require non-empty nextCursor>')
```

Results: registration returned HTTP 201; two posts were created and saved;
the first saved page returned HTTP 200 with one item and a non-empty opaque
`nextCursor`.

Only the API container was then recreated; the project Data Protection volume
was retained:

```bash
docker compose -p fookbase-m11-smoke \
  -f compose.yml -f /tmp/fookbase-m11-smoke.ports.yml \
  up -d --force-recreate api
until curl -fsS http://localhost:15000/health/ready >/dev/null; do sleep 2; done
curl -fsS --get -H "Authorization: Bearer $ACCESS_TOKEN" \
  --data-urlencode "cursor=$CURSOR" --data 'limit=1' \
  'http://localhost:15000/api/posts/saved'
curl -fsS -H "Authorization: Bearer $ACCESS_TOKEN" \
  'http://localhost:15000/api/posts/saved?limit=1'
```

Results: Compose reported `fookbase-m11-smoke-api-1 Recreated`; readiness
succeeded after recreation. The request using the exact same opaque cursor
returned HTTP 200 with one item, with no cursor or protection error. A fresh
authenticated saved-page request also returned HTTP 200 with one item. Normal
behavior therefore remained healthy. One transient `curl: (56) Recv failure:
Connection reset by peer` occurred during the readiness polling while the
recreated API was beginning to listen; polling then succeeded and both
subsequent functional requests were normal.

## React warning audit

Existing commands were run for every frontend package:

```bash
npm --prefix frontend/web run lint
npm --prefix frontend/web run build
npm --prefix frontend/messenger run lint
npm --prefix frontend/messenger run build
npm --prefix frontend/admin run lint
npm --prefix frontend/admin run build
```

Initial result: all three production builds succeeded, and messenger/admin
lint had no warnings. `frontend/web` lint emitted exactly these two warnings:

```text
src/pages/hashtags/HashtagPage.tsx:38:26: warning react(set-state-in-effect): Calling setState synchronously within an effect can trigger cascading renders help: Effects should synchronize React with external systems. Calling setState synchronously inside an effect starts another render and is usually unnecessary. Derive the value during render, initialize state directly, or update it from the event that caused the change. Use an effect only when synchronizing with an external system.
src/pages/saved/SavedPostsPage.tsx:35:26: warning react(set-state-in-effect): Calling setState synchronously within an effect can trigger cascading renders help: Effects should synchronize React with external systems. Calling setState synchronously inside an effect starts another render and is usually unnecessary. Derive the value during render, initialize state directly, or update it from the event that caused the change. Use an effect only when synchronizing with an external system.
```

Both locations are Fookbase application pages, not dependencies. The initial
effect called the page `load` callback synchronously; that callback immediately
updates page loading state. The focused fix in commit `ee8d9c5` schedules the
initial callback through a resolved Promise in each page, so the effect itself
does not synchronously set state. Fetching, retry, pagination, and rendering
are otherwise unchanged.

The rule was verified as an error before and after the fix:

```bash
cd frontend/web
npm exec -- oxlint --deny react/set-state-in-effect \
  src/pages/hashtags/HashtagPage.tsx src/pages/saved/SavedPostsPage.tsx
npm run lint
npm run build
```

Before the change, the first command failed with the two exact diagnostics
above. After the change, it exited successfully with no diagnostics; web lint
and build also exited successfully with no warnings.

## Teardown

The disposable project was removed using the requested project name:

```bash
docker compose -p fookbase-m11-smoke down -v --remove-orphans
test -z "$(docker ps -aq --filter label=com.docker.compose.project=fookbase-m11-smoke)"
test -z "$(docker volume ls -q --filter name='^fookbase-m11-smoke_')"
```

Result: success; zero containers and zero named volumes remained for the smoke
project, including the Data Protection key-ring volume. The temporary port
override was outside the repository and was deleted after teardown. No source
artifacts, key material, or disposable data remain in the repository.

## Review fix round 1: initial request lifecycle

Review identified that the prior `Promise.resolve().then(() => load())` change
only deferred a synchronous loading/error state update and did not make the
request lifecycle safe. That microtask workaround has been removed from both
pages.

`HashtagPage` now derives initial loading from `settledTag !== tag`, which is
true on first render without a synchronous effect update. On a hashtag-route
replacement it remains true until the new tag request settles. `SavedPostsPage`
continues to derive initial loading from its existing `useState(true)` value.
Initial requests are now local effect functions that update UI state only after
their request resolves or rejects.

Each saved/hashtag request creates an `AbortController`; the API helpers pass
its signal to `apiRequest`. A new request aborts the old one, effect cleanup
aborts on unmount/replacement, and every response checks both the abort signal
and its controller identity before updating state. Retry and pagination retain
their existing controls and page merge behavior; their loading state is cleared
only by the request that is still current.

The web package has no `test` script or installed test runner (its scripts are
`dev`, `build`, `lint`, and `preview`), so no unit-test command exists without
adding unrelated tooling. The focused validation commands and their exact
successful output summaries were:

```bash
cd frontend/web
npm run lint
npm exec -- oxlint --deny react/set-state-in-effect \
  src/pages/hashtags/HashtagPage.tsx src/pages/saved/SavedPostsPage.tsx
npm run build
! rg -n 'Promise\.resolve\(\)\.then' \
  src/pages/hashtags/HashtagPage.tsx src/pages/saved/SavedPostsPage.tsx
```

```text
npm run lint
> oxlint

npm exec -- oxlint --deny react/set-state-in-effect ...

npm run build
> tsc -b && vite build
✓ 111 modules transformed.
✓ built in 328ms
```

All four commands exited 0 with no lint diagnostics. Source diff was also
checked with:

```bash
git diff --check -- src/api/posts.ts src/pages/hashtags/HashtagPage.tsx src/pages/saved/SavedPostsPage.tsx
```

Self-review: no render reads a mutable ref; no `Promise.resolve().then` remains;
the initial effects have no synchronous `setState` before `await`; stale,
aborted, unmounted, and route-replaced requests cannot commit response state;
and the new optional `AbortSignal` arguments preserve every existing API call
signature. This review-fix commit was intentionally not pushed per controller
instruction.
