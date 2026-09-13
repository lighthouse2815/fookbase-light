# Memories, Birthdays, and Profile Extras V1

## Memories

`GET /api/memories/today` is private to the signed-in owner. It projects existing, active Standard posts from that owner's Profile container on the same server-local month/day in prior years. Group/page/event content, reels, current-year posts, and deleted posts are excluded. No memory copy or memory-share record is stored.

## Birthdays

Profiles keep an optional date-only birth date and a visibility choice: OnlyMe, Friends, or Public. Owners receive the full saved date; other profile viewers receive only month/day when the selected visibility permits it. Birthday lists only include active friends without a block in either direction.

`GET /api/birthdays/today` returns today's visible friend birthdays. `GET /api/birthdays/upcoming?days=7` returns the next 1–30 days and handles the December/January boundary. A Feb 29 birthday is shown on Feb 28 in non-leap years.

## Profile extras

The profile editor supports current city, hometown, workplace, education, and a HTTP(S) website. Website URLs are validated server-side and rendered with `noopener noreferrer` in the web client.

## Deferred

V1 has no automatic memory or birthday notifications, calendar browsing, AI resurfacing, custom birthday audiences, profile employment/education history, birthday commerce, or feed/search changes.
