# Events V1

Events use the shared `FookbaseDbContext` and PostgreSQL database. Timestamps are stored as UTC; the web client sends an offset-aware local timestamp and renders it in the browser's local timezone.

An event is hosted by a user, Group, or Page. The authenticated user remains the audit creator. User hosts are managed by that user; Group owners, admins, and moderators may manage Group events; Page owners, admins, and editors may manage Page events. Page and Group display identities never expose a manager as the social host.

Published public events are discoverable. Private events are limited to managers, invitees, and participants. Drafts are manager-only, cancelled events are read-only, and deletion is soft deletion. Online events require an `http` or `https` URL.

RSVP is explicit: creators and managers are not automatically participants. A viewer may select Going or Interested, or remove their response. Invitations are manager-only; accepting creates or updates a Going RSVP atomically.

Event discussion uses normal Posts with `ContainerType = Event`; the real user remains the post author. Public discussions are readable by authenticated event viewers and writable by participants/managers. Private discussion follows event access, and cancelled events are read-only.

Cover media must be a ready image owned by the acting manager. An event cover reference blocks deletion while it is attached, and access uses the existing short-lived media URL flow.

Deferred from V1: tickets/payments, recurring events, calendar sync, livestreaming, home-feed insertion, recommendations, and a full Profile/Group/Page event tab redesign.
