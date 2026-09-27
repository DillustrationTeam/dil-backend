# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Read this first

This repo enforces a documented-context workflow for every AI agent (Claude, Copilot, Cursor). Before making non-trivial changes, read `AGENTS.md` and the relevant files under `docs/` in the order listed in `AGENTS.md`:

1. `docs/00-overview.md` — product scope, actors, out-of-scope boundaries
2. `docs/01-architecture.md` — Clean Architecture layout, module boundaries
3. `docs/02-workflow.md` — branch/commit/PR conventions, pre-PR checklist
4. `docs/03-database.md` — schema, indexes, concurrency rules, migration commands
5. `docs/04-api-conventions.md` — REST envelope, auth, SignalR, rate limiting
6. `docs/05-use-cases.md` — 7 actors, multi-role user model, 34 use case specs
7. `docs/07-payos-integration-guide.md` — payOS webhook setup (read before running payment module locally)

If a change would contradict something already decided in `docs/` (e.g. splitting into microservices, adding a second DbContext strategy, sharding), stop and ask rather than silently diverging. If a new architectural decision gets made during a session, update the relevant `docs/` file immediately — don't let it live only in chat history.

**Docs are in Vietnamese.** Match that when editing `docs/`; code, identifiers, and comments stay in English.

## Commands

```bash
# Build
dotnet build

# Run the API (https://localhost:7081, Swagger UI at /swagger in Development)
dotnet run --project src/ArtCommission.API

# EF Core migrations (Infrastructure holds DbContext + Migrations, API is the startup project)
dotnet ef migrations add <Name> -p src/ArtCommission.Infrastructure -s src/ArtCommission.API
dotnet ef database update -p src/ArtCommission.Infrastructure -s src/ArtCommission.API

# CI build gate: must be 0 errors, 0 warnings (warnings are treated as errors)
dotnet build --no-restore --configuration Release -warnaserror
```

Local first-time setup: copy `src/ArtCommission.API/appsettings.Example.json` to `appsettings.Development.json` and fill in real connection string / JWT secret before running.

### Tests

There is no xUnit/NUnit suite. `tests/CommissionWorkflowChecks` is a runnable console program that exercises the commission state machine (accept → deposit → submit milestone → approve → escrow release, plus rejected-transition checks) against EF Core InMemory providers:

```bash
dotnet run --project tests/CommissionWorkflowChecks
```

It throws on the first failed assertion, so a clean exit means all checks passed.

## Architecture

Monolith, Clean Architecture, 4 projects in 1 solution, 1 deployable (`ArtCommission.sln`). SDK pinned via `global.json` (8.0.421).

```
Domain          <- no project references
Application     <- depends on Domain
Infrastructure  <- depends on Domain + Application
API             <- depends on Application + Infrastructure
```

Dependencies point inward only. `ArtCommission.Domain` must stay at 0 project references.

```
src/
  ArtCommission.API/
    Controllers/
    Hubs/                    # NotificationHub (SignalR), mapped at /hubs/notifications
    Middleware/
    BackgroundWorkers/       # e.g. PaymentReconciliationWorker (hosted service)
    Program.cs                # DI composition root — start here to see what's wired up
  ArtCommission.Application/
    Common/Interfaces/       # cross-module contracts (IApplicationDbContext, IIdentityService, IPaymentGateway, ...)
    Common/Behaviors/
    Auth/ ArtistStudio/ Commission/ Payment/ Chat/ Admin/ Notifications/ CreatorApplication/ Marketplace/ Wallets/
  ArtCommission.Domain/
    Common/BaseEntity.cs     # Id (Guid), CreatedAt, UpdatedAt, IsDeleted
    Entities/{Identity,ArtistStudio,Commission,Payment,Chat,Admin}/
    Enums/
  ArtCommission.Infrastructure/
    Persistence/              # AppDbContext, ApplicationDbContext, Configurations/, Migrations/
    Repositories/
    ExternalServices/{Cloudinary,Vnpay,Momo,PayOs,OpenAi,AzureSearch}/
    BackgroundJobs/
```

### Module boundary rule

The per-layer module folders (Identity/Auth, ArtistStudio, Commission, Payment, Chat, Admin) are organizational, not separate projects. A module must not call another module's Repository/DbContext directly — go through an interface declared in `Application/Common/Interfaces`. This is a deliberate simplification over a true modular monolith (small team, short project timeline); don't propose splitting into per-module projects or microservices without asking first.

### DbContext note

`docs/01-architecture.md` states there is a single `AppDbContext` by design (so Commission + Payment flows like "approve milestone + release escrow" stay in one atomic transaction). As currently wired in `Program.cs`, the codebase actually registers **two** contexts — `AppDbContext` (Identity/Auth + most domain entities) and `ApplicationDbContext` (Commission service, falls back to EF InMemory if no connection string is set). Be aware of this drift when touching persistence: don't assume operations across both contexts are atomic, and don't add a third context to "match the pattern" without checking which one new entities actually belong in.

### Background jobs

Hangfire-style/hosted-service background work (escrow auto-release timeout, refresh token cleanup, payment reconciliation) lives in `Infrastructure/BackgroundJobs/` and `API/BackgroundWorkers/`, using SQL Server storage rather than new infra.

## API conventions

- REST only, versioned via URI: `/api/v1/...` (no header/query versioning, no GraphQL/tRPC).
- Every response uses the envelope `{ "data": ..., "meta": ..., "error": null }` — never a raw array/object at the top level, including empty lists and error responses.
- Errors follow ASP.NET Core `ProblemDetails` shape inside `error` (`type`, `title`, `status`, `detail`, `traceId`; validation errors add `errors` per field). HTTP status must match `error.status`. Built centrally by `ApiErrors` / the exception-handler middleware in `Program.cs` — don't hand-roll error responses in controllers.
- Auth: JWT Bearer, 15 min access / 7 day refresh, refresh tokens rotate on every use and are stored as SHA-256 hashes (never plaintext) in `RefreshTokens`; reuse of a revoked token revokes the whole chain.
- SignalR hub `/hubs/notifications`; JWT passed via query string (`?access_token=`) at handshake since WebSocket upgrade can't carry an Authorization header — this is only honored for `/hubs` paths, not REST.
- Pagination: keyset/cursor for marketplace listings and chat history (`SentAt`-ordered) — no deep `OFFSET`.
- New endpoints need Swagger annotation (`[ProducesResponseType]` or XML doc) — Swashbuckle generates the OpenAPI spec from code; there is no hand-written spec to keep in sync.
- Rate limiting via built-in `Microsoft.AspNetCore.RateLimiting`, fixed-window on public-sensitive endpoints (auth, search).

## Database

SQL Server + EF Core 8, database-first in intent (SQL DDL drives schema, then mapped to entities). Every entity except join tables/append-only logs inherits `BaseEntity` (`Id: Guid`, `CreatedAt`, `UpdatedAt`, `IsDeleted` soft-delete flag).

- Identity: `AspNetUsers` extends `IdentityUser<Guid>`; multi-role via `AspNetUserRoles`/`AspNetRoles` (a user can hold both `Client` and `Creator` roles simultaneously — this is the standard "multi-role user" model, not a bug).
- Locked-in indexes (don't casually add/remove): `AspNetUsers(NormalizedEmail)`/`(NormalizedUserName)` unique, `RefreshTokens(TokenHash)` unique, `Artworks(CreatorId, CreatedAt DESC)`, `Commissions(Status, UpdatedAt)` filtered on active statuses, `Messages(CommissionId, SentAt DESC)`, `Wallets(UserId)` unique.
- No SQL full-text index on artwork descriptions — search/semantic matching is delegated to Azure AI Search (dual-write on artwork create/update).
- `Wallet`/`Payment`/`Transaction` are an append-only ledger with a `RowVersion` concurrency token to guard race conditions on deposits/withdrawals.
- Payment gateway (VNPAY/MoMo/payOS) webhooks must be idempotent via `TransactionRef` — guard against double-credit/double-release on retry.
- Index/scaling strategy (no sharding, no read replicas, no partitioning) is locked in `docs/03-database.md` — don't introduce these without an explicit ask.

## Workflow conventions

- Branch naming requires a dev id: `feature/<dev-id>-<module>-<short-desc>`, `fix/<dev-id>-<module>-<short-desc>`, `chore/<dev-id>-<short-desc>`. Valid `<module>` values: `auth`, `artist-studio`, `marketplace`, `commission`, `payment`, `chat`, `admin`.
- Commits are Conventional Commits (`feat`, `fix`, `docs`, `refactor`, `test`, `chore`, with module scope in parens).
- **Do not add a `Co-authored-by` line to commits in this repo** — `docs/02-workflow.md`/`CONTRIBUTING.md` explicitly forbid it, overriding the default attribution trailer.
- Schema changes require running `dotnet ef migrations add` before pushing (not left for someone else), with a migration name that describes the change — avoids model-snapshot conflicts between branches.
- Before opening a PR: `dotnet build` clean with no new warnings, migrations tested end-to-end with `dotnet ef database update`, no `bin/`/`obj/`/real secrets committed, new endpoints have Swagger annotations.
- No self-merge; changes touching `Common/` (Interfaces, Behaviors, BaseEntity) require review since they affect every layer above.

## Explicit non-goals

Don't build: direct art purchases outside the Commission flow, automated third-party dispute resolution (admin/moderator handle disputes manually), a native mobile app, non-Vietnam-market payment/localization. This repo is backend-only — no frontend UI code (that's `dil-frontend`, a separate repo).
