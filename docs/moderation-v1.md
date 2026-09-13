# Moderation V1

`ContentReport` remains the moderation case. It records the reporter, `User` or `Post` target, reason, details, lifecycle (`Pending`, `Reviewed`, `Dismissed`, with legacy `Resolved` retained), and resolution time. Reports are never deleted by review.

Admin decisions create immutable `ModerationAction` records. These include the report where applicable, subject account, target, action, public-safe reason, private admin note, moderator identity, timestamp, and an expiry for temporary suspension. Internal notes and moderator/reporter identity are never exposed to normal users.

`UserModerationState` is deliberately separate from ASP.NET Identity lockout. It holds warning count, temporary suspension, disabled state, and update time. A suspension or disable revokes active sessions and refresh tokens. Login, two-factor completion, and refresh reject moderated accounts. Existing short-lived JWTs are not instantaneously invalidated; authenticated non-Admin write requests are centrally rejected until the token expires. Read access remains available where current endpoint policy permits.

Content removal uses the existing Post soft-delete path, so removed posts disappear through normal feed/search visibility checks. M17 does not add new reportable target types: the audited V1 report surface is User and Post (Reels are Posts). Historical content remains after an account disable unless separately removed.

Admin endpoints include report detail, dismiss, remove reported content, warn/suspend from a report, direct account warn/suspend/unsuspend/disable/enable, moderation state, and cursor-paginated account action history. The existing offset report endpoint remains backward compatible; supplying a moderation cursor or target filter uses the oldest-first keyset queue response.

The Admin frontend extends its existing report queue and account list with dismiss/remove/warn/suspend controls and account warning, suspension, disable, enable, and unsuspend controls. Confirmation is required for suspension and disable.

Deferred from V1: appeals, automated enforcement, AI/ML moderation, moderator hierarchy/assignment, IP/device bans, strikes escalation, and private-message content moderation.
