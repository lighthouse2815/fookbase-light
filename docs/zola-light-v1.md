# Zola Light V1

`frontend/zola-light` is a separate Vite application, backed by the existing
Fookbase monolith and its `FookbaseDbContext`. It does not introduce another
database, service, queue, or identity provider.

## Authentication and deployment

Zola Light uses the normal Identity login and refresh APIs at its own origin.
It never accepts a token in a URL or query string. In production, deploy the
web and Zola Light applications on origins explicitly listed in
`Cors:AllowedOrigins`; configure `VITE_API_BASE_URL` and `VITE_WEB_URL` for
Zola Light, and `VITE_ZOLA_LIGHT_URL` for the main web navigation.

An independent login is intentional: browser local storage is origin-scoped,
so local development on ports 5173 and 5175 cannot safely share the main web
session without an additional, security-sensitive SSO/cookie design.

## Conversation access and block rule

Direct conversations require friendship and reject a block in either direction
for creation, history, sending, reactions, read cursors, typing and realtime
delivery. A stored Direct conversation is not a bypass after a later block.

Groups do not force a member to leave merely because a later block exists.
Instead, V1 filters the blocked pair in both directions: their messages do not
appear in one another's history and no Message, Typing, Presence, or read
realtime event is delivered across that pair. There are no user mentions in
Zola Light V1. Other group participants continue normally.

The same filter applies to conversation previews, search, reply previews,
reaction lists, attachment read URLs, reaction operations and message-specific
realtime events. A message ID does not grant access to a message sent by a
blocked member.

Active members can view the full retained group history that they are permitted
to see. A member who leaves or is removed loses all API and future realtime
access, including historical access; rejoining creates a new active interval
and begins its unread cursor at the newest existing message.

## Participant and reaction state

`ConversationParticipant` is the source of truth for membership, authorization,
read/delivery cursors and the Direct conversation peer used by runtime access
checks. The nullable `Conversation.UserId1`/`UserId2` pair remains only for
legacy Direct migration, the canonical-pair unique constraint and race-safe
Direct creation; it is not used as a runtime membership or cursor authority.

`MessageReactions` has a primary key on `(MessageId, UserId)`: a participant has
one active reaction per message. Setting a different type replaces the prior
reaction; deleting the reaction removes that single row.

## Realtime and presence

SignalR accelerates UI updates only; the HTTP API/database remains authoritative
and Zola Light reloads conversations and history after reconnect. Typing is
ephemeral. Presence uses in-memory authenticated connection counts and is thus
correct for one backend instance only. A Redis SignalR backplane and distributed
presence are deliberately deferred until multi-instance deployment is needed.
