# Modular-monolith architecture decision

## Scope

Fookbase runs as one ASP.NET Core process, `Fookbase.Api`, on port 5000. Identity, Users, Friends, Posts, and Media remain independent code modules, but they are not independently deployed services.

`Fookbase.Api` is the sole composition root and the only backend project. It registers each module through `AddIdentityModule`, `AddUsersModule`, `AddFriendsModule`, `AddPostsModule`, and `AddMediaModule`. Module code is organized under `backend/Fookbase.Api/Modules/<Module>`; HTTP endpoints live in each module's `Endpoints` folder.

External local dependencies are PostgreSQL and MinIO. There is no API gateway, RabbitMQ, service discovery, distributed transaction, or HTTP communication between application modules.

## Module boundaries and communication

Each module retains feature-local `Entities`, `Services`, `Repositories`, and `Endpoints` folders inside the single API project. A module must not access another module's `DbContext` or `DbSet`.

Cross-module communication uses explicit application abstractions or typed integration-event contracts. The current Posts-to-Media read URL path is an in-process call: `DirectMediaReadUrlClient` adapts Posts' `IMediaReadUrlClient` to Media's `IMediaService`; it does not create an HTTP request or use an internal service token.

Events are dispatched by `InProcessIntegrationEventPublisher` in the API project. Event-type strings remain contract identifiers for durable records and idempotency; broker-specific exchange, queue, and routing-key constants were removed.

## Projection audit

The following projections are intentionally retained:

| Projection | Owner | Reason |
| --- | --- | --- |
| `Posts.KnownUsers` | Posts | Lets Posts validate a known author without reading Identity persistence directly. |
| `Friends.KnownUsers` | Friends | Supports relationship validation and local relationship responses without coupling Friends to Identity storage. |
| `Media.KnownUsers` | Media | Keeps media ownership validation within Media's database boundary. |
| `Posts.KnownMedia` | Posts | Validates attachment ownership, readiness, and deleted state before post mutations. |
| `Posts.FriendEdges` and `Posts.BlockedEdges` | Posts | Feed and privacy queries filter many posts; local read models avoid per-post cross-module calls and preserve a single query path. |

No projection is retained solely to simulate a microservice. They are local read models needed to preserve module persistence ownership and to make feed/privacy/media authorization efficient. Replacing them with direct application contracts would require cross-module calls on every feed candidate and would make the current query behaviour slower and more complex.

## Outbox and inbox decision

Outbox and inbox remain for mutations that update another module's persistent read model:

1. A source module saves its business state and its outbox message in its own database transaction.
2. An in-process hosted worker retries pending messages after failures or process restarts.
3. A target module records the event in its inbox with its projection update, making repeated delivery idempotent.

This is not distributed messaging: all handlers run inside the same API process. It is retained because the modules currently own separate `DbContext` transactions; removing it without adding a cross-context transaction would allow permanent projection drift if the process stopped between source and target writes.

Events with no current consumer are retained in the source outbox as audit records. They do not create queues or invoke a broker.

## Database consolidation plan

The target is one PostgreSQL database named `fookbase_db`, with schemas owned by modules:

```text
identity.*
users.*
friends.*
posts.*
media.*
```

Each module will keep its own `DbContext` and EF migrations. The current five development databases are intentionally not reset or migrated automatically.

Safe migration milestone:

1. Back up and verify all five source databases; record row counts and migration history.
2. Create `fookbase_db` and the five schemas without modifying source databases.
3. Add schema-aware EF migrations for fresh installations, then apply them to the target database.
4. Copy each module's tables from its source database to its matching target schema in one maintenance window, preserving primary keys, timestamps, outbox, and inbox records.
5. Validate row counts, foreign-key/check constraints, pending outbox count, and representative API flows against the target.
6. Switch connection strings only after validation; retain source backups until rollback is no longer needed.

A migration script must be introduced with the schema migrations in that dedicated milestone. It must require explicit source and target connection strings, run read-only preflight checks first, and never drop, reset, or overwrite a source database.

## Compatibility

All public routes remain under `/api/auth`, `/api/users`, `/api/friends`, `/api/posts`, and `/api/media`, served by `http://localhost:5000`. JWT and ASP.NET Core Identity are unchanged; endpoints continue to take the actor identifier from the JWT `sub` claim.

Media remains private in MinIO. Clients upload with presigned PUT URLs, Posts obtains signed read URLs through the in-process Media call, and ownership/reference/lifecycle/signature validation remain in Media.
