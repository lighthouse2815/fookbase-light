# Modular-monolith architecture decision

## Scope

Fookbase runs as one ASP.NET Core process, `Fookbase.Api`, on port 5000. Identity, Users, Friends, Messages, Posts, and Media remain independent code modules, but they are not independently deployed services.

`Fookbase.Api` is the sole composition root and the only backend project. It registers each module through `AddIdentityModule`, `AddUsersModule`, `AddFriendsModule`, `AddMessagesModule`, `AddPostsModule`, and `AddMediaModule`. Module code is organized under `backend/Fookbase.Src/Main/Code/Modules/<Module>`; HTTP endpoints live in each module's `Endpoints` folder and EF migrations live in `Data/Migrations`.

External local dependencies are PostgreSQL and MinIO. There is no API gateway, RabbitMQ, service discovery, distributed transaction, or HTTP communication between application modules.

## Module boundaries and communication

Each module keeps feature-local `Entities`, `Data`, `Services`, and `Endpoints` folders inside the single API project. A module does not access another module's `DbContext` or `DbSet`.

Cross-module coordination is explicit and synchronous: an endpoint calls an application use case only when it must combine services. `RegistrationUseCase` creates the authentication account and its user profile. `PostsUseCase` obtains the current Friends relationship snapshot for privacy checks and asks Media to validate and synchronize post attachments. Messages uses `FriendsService` for direct-conversation access checks and SignalR at `/hubs/messages` for client updates. These are in-process C# calls, not HTTP requests.

## No messaging projections

There is no messaging layer, integration-event contract, outbox, inbox, event publisher, projection worker, or hosted outbox service. Posts reads current relationship data through `FriendsService`; Media remains the owner of attachment metadata and references. The direct calls are intentionally simple for this single-process application.

## Database consolidation plan

The target is one PostgreSQL database named `fookbase_db`, with schemas owned by modules:

```text
identity.*
users.*
friends.*
messages.*
posts.*
media.*
```

Each module will keep its own `DbContext` and EF migrations. The current five development databases are intentionally not reset or migrated automatically.

Safe migration milestone:

1. Back up and verify all six source databases; record row counts and migration history.
2. Create `fookbase_db` and the six schemas without modifying source databases.
3. Add schema-aware EF migrations for fresh installations, then apply them to the target database.
4. Copy each module's tables from its source database to its matching target schema in one maintenance window, preserving primary keys and timestamps.
5. Validate row counts, foreign-key/check constraints, and representative API flows against the target.
6. Switch connection strings only after validation; retain source backups until rollback is no longer needed.

A migration script must be introduced with the schema migrations in that dedicated milestone. It must require explicit source and target connection strings, run read-only preflight checks first, and never drop, reset, or overwrite a source database.

## Compatibility

All public routes remain under `/api/auth`, `/api/users`, `/api/friends`, `/api/messages`, `/api/posts`, and `/api/media`, served by `http://localhost:5000`; SignalR messages are served at `/hubs/messages`. JWT and ASP.NET Core Identity are unchanged; endpoints continue to take the actor identifier from the JWT `sub` claim.

Media remains private in MinIO. Clients upload with presigned PUT URLs; PostsUseCase obtains signed read URLs through the in-process Media service, and ownership/reference/lifecycle/signature validation remain in Media.
