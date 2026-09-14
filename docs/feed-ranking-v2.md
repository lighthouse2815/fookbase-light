# Feed Ranking V2

## Scope

Ranking V2 is deterministic and runs entirely in the .NET monolith with PostgreSQL. It does not use ML, embeddings, a vector store, queues, or a persisted affinity/feed cache.

## Candidate sources

Home Feed keeps the existing bounded per-source candidate windows and authorization rules:

- the viewer's Profile content;
- followed Profile creators (Friends-only visibility still requires friendship);
- joined Group posts;
- followed, published Page posts;
- existing eligible shares;
- public Profile Reels and public Profile standard posts from unfollowed creators as conservative suggestions.

Following Feed remains chronological, organic-only, and has no suggestions.

## Score

For a Home candidate, the integer tick score is conceptually:

`relationship base + creator affinity + Group/Page affinity + Reel-watch affinity + capped engagement - age`

Relationship bases retain the V1 own/followed/Group/Page values. One relationship or affinity point offsets the configured number of freshness hours. Engagement is intentionally weaker: each normalized engagement point adds configurable minutes, rather than a full affinity point. Ties use score, then creation time, then ID.

All contribution categories are capped. This bounds repeat activity, keeps configuration understandable, and prevents a popular item from overwhelming relationship signals.

## Affinity and lookback

Affinity is directional: it is calculated for the requesting viewer against candidate Profile creators and eligible Group/Page sources. PostgreSQL groups only the viewer's reactions, comments, saves, shares, source posts, and Reel views within `InteractionLookbackDays` (60 days by default) and at or before `AsOfUtc`.

Comments weigh more than reactions by default. Saves remain private behavior and are used only for the viewer's own ranking. Group/Page affinity is only applied after membership/follow authorization has selected that source.

Reel views use the existing validated duration and completion data. A completed or configured strong-completion view boosts the relevant Profile creator; repeated short exits can apply only a bounded, modest reduction.

## Content engagement

Reactions, active comments, and active shares on each bounded candidate set are grouped in PostgreSQL. Counts are normalized with `EngagementNormalizationCap`, then converted using the lower `EngagementMinutesPerPoint` signal. Engagement is never an authorization signal.

## Suggestions

Suggestions are limited to public, accessible Profile Reels and standard posts by unfollowed creators. Private Groups, Friends/OnlyMe content, Events, Stories, Messenger, and moderation content are never suggested. The existing quota inserts at most one suggestion after the configured number of organic items (four by default). The protected cursor preserves the quota counter and the last suggested creator, avoiding consecutive suggestions from the same creator.

Suggested items expose only the broad `recommendationReason` value `Suggested for you`; scores and other users' interaction details are not exposed.

## Privacy and session stability

Authorization and current relationship checks run before ranking/projection, and run again on every cursor request. Blocks, deleted/removed content, container membership, Page publication, ready Reel media, and privacy rules therefore override every score.

A new session fixes `AsOfUtc`. Ranking aggregates include only activity at or before that value, so later content and interactions do not reshuffle a continued session. Feed cursor version 2 is viewer-, mode-, configuration-hash-, and snapshot-bound; V1 cursors are rejected normally.

## Query strategy and database

Feed first loads bounded candidates per existing source. It then executes a fixed, small batch of aggregate queries for reactions, comments, saves, shares, viewer source activity, Reel views, and candidate engagement; it never performs an affinity query per item, author, Group, or Page. The V2 migration adds only three supporting indexes for viewer reaction, comment-author, and share-author lookback queries. No ranking table is added.

## Configuration and deferred work

`FeedRanking` contains relationship bases, affinity/engagement weights and caps, Reel thresholds, the lookback window, candidate window, and suggestion quota. Changing this configuration changes the protected ranking version and invalidates old continuation cursors.

ML training, embeddings, vector search, Redis/distributed cache, queues, persisted feeds, and scaling infrastructure are intentionally deferred.
