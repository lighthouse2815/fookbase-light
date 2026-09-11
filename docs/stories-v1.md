# Stories V1

Stories are a dedicated module in the existing ASP.NET Core monolith and use
the existing `FookbaseDbContext`, Media, Friends, Notifications and Messages
modules. They are not Posts and do not introduce another database or worker.

## Lifecycle and visibility

The server creates `ExpiresAtUtc` from `CreatedAtUtc + Stories:LifetimeHours`;
the default is 24 hours. A Story is active only when it has not been deleted
and `ExpiresAtUtc` is later than the current UTC time. There is no per-Story
scheduler: expiration is evaluated in the query path.

An active Story is visible to its author, or to a non-blocked viewer permitted
by `public` / `friends` privacy. The Home tray intentionally includes only the
current user and current friends; it never uses public Stories for discovery.
Expired Stories are private to the author's archive. A block in either
direction hides a Story and its media, views, reactions, replies and Messenger
preview.

## Media and references

A Story has one owned ready Media asset. It accepts a ready image, or a ready,
fully processed video from the existing FFmpeg pipeline. `Media:MaximumStoryVideoDurationMs`
defaults to 60,000 milliseconds.

`StoryMediaReferences` protects media for every non-deleted Story, including
archived expired Stories. `DELETE /api/media/{id}` returns a conflict while the
reference exists. Deleting a Story soft-deletes it and releases the reference;
expiration alone never deletes media.

## APIs

- `GET /api/stories` returns the authenticated Home tray, grouped by author.
- `POST /api/stories` accepts `mediaId`, optional `caption`, and `privacy`.
- `GET /api/stories/{id}` returns one active authorized Story.
- `POST /api/stories/{id}/view` is idempotent; authors are not counted.
- `GET /api/stories/{id}/viewers?cursor=&limit=` is newest-first and owner-only.
- `POST` / `DELETE /api/stories/{id}/reaction` upsert/remove one reaction per
  viewer. A non-self reaction queues a `StoryReaction` notification.
- `POST /api/stories/{id}/reply` sends a normal Direct Messenger message with
  `Message.StoryId`; existing friend and block rules still apply.
- `GET /api/stories/{id}/media/access` and `/media/poster/access` issue
  short-lived private MinIO URLs. Only an author using Archive can access
  expired media.
- `DELETE /api/stories/{id}` is author-only and releases the media reference.
- `GET /api/stories/archive?cursor=&limit=` returns the owner's expired,
  non-deleted Stories.

Message responses include a `story` reference. It contains compact metadata
only when the current message viewer can still access an active Story;
otherwise it is an unavailable state and contains no media URL or caption.

## Frontends

The web Home page fetches tray metadata only. The viewer obtains a signed media
URL only when that Story becomes active. It supports sequential author playback,
image timing, video progress, pause/resume, keyboard and swipe/click navigation,
reactions, replies and the owner's viewer list. The Archive page is private.
Messenger renders a compact Story reference or a safe unavailable state.

## Deferred

Highlights, Close Friends, custom audiences, Memories, resharing, HLS, CDN and
ML ranking are intentionally outside Stories V1.
