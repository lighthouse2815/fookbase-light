# Notification Deep Links V1.1 Design

**Status:** Approved in chat on 2026-09-15

## Goal

Make every emitted notification type display safely and send the recipient to its precise, authorized destination without requiring frontend lookup chains.

## Scope

- Synchronize web notification type/entity unions with all backend enum values.
- Render Vietnamese copy for event and account-warning notifications without exposing moderator, reporter, or internal-note data.
- Add one frontend notification presentation/destination resolver used by both notification surfaces.
- Expose an optional, safe `parentEntityId` in notification responses. For a Comment it is the owning post; for `EventInvite` it is the invitation's event. Other event notifications continue to use their entity ID.
- Add `/posts/:postId`, reusing `postsApi.getById` and `LivePostCard`; display the Vietnamese unavailable state for 403/404.

## Non-goals

- No notification database column or EF migration.
- No change to existing notification visibility, block, post, comment, event, group, page, or friend authorization rules.
- No generic feed fallback for precise Post, Comment, or Event notification targets.
- No change to localStorage token persistence; its migration remains separate security-hardening work.

## Backend projection

`NotificationResponse` gains nullable `ParentEntityId` after `EntityId`. `NotificationService.ToResponsesAsync` resolves parent values in bounded set queries for the notification page/realtime payload:

- Comment notifications: query active comment IDs to `PostId` and set the matching response parent.
- `EventInvite`: query the invitation ID to `EventId` and set the matching response parent.
- all other notifications: leave the field null.

Existing `CanSurfaceAsync` remains the authority for visibility. The projection is calculated only after a notification is eligible to surface, and it adds no profile or moderation details.

## Frontend routing and presentation

Create one notification helper that exports the notification text, visual badge, and `getNotificationDestination`.

| Notification target | Destination |
| --- | --- |
| Post | `/posts/{entityId}` |
| Comment | `/posts/{parentEntityId}#comment-{entityId}` |
| Event or EventInvite | `/events/{parentEntityId ?? entityId}` |
| Friend request/follow | existing actor profile surface |
| Group/Page invitation | existing group/page surfaces |
| Account warning | `/settings/security` |
| Missing optional target | `/notifications` |

Both `NotificationsPage` and `TopNavbar` consume this helper, eliminating their separate switch logic. A post detail page loads exactly one post through the existing API, passes it to `LivePostCard`, and maps a 403/404 to `Nội dung này không còn khả dụng.`. It does not relax backend authorization.

## Verification

- Add focused integration coverage for Comment and EventInvite parent projection, plus inaccessible target filtering if the test fixture supports it.
- Run the focused Notifications tests and the backend regression script after backend changes.
- Run `npm ci`, `npm run lint`, and `npm run build` in `frontend/web`.
- Manually verify Post, Comment, Event, AccountWarning, and unavailable-target paths.
