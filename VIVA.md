# Viva Prep — Ride Sync (ASP.NET Core MVC + Redis)

## 30-second pitch
"Ride Sync is an ASP.NET Core MVC ride-hailing app with Redis. The interesting engineering is correctness under
concurrency: if 30 drivers press *Accept* on the same ride at once, exactly one wins. My **Race Lab** page proves it live —
the buggy version tells 30/30 drivers they won, the real one tells 1. I also added a Redis Lua sliding-window rate limiter,
cache-aside for the hot driver page, and idempotency keys against double-submits."

## MVC questions
**Q: Explain MVC in your project.** Model = entities, view models and business rules (`Models/`, `Services/`). View = Razor `.cshtml`,
only presentation. Controller = receives the HTTP request, validates/binds input, calls a service, chooses the view or redirect.
Controllers are deliberately thin; services never reference HTTP (they return `Result<T>` with an `Outcome`), so they're testable without a web server.

**Q: Why a service layer on top of MVC?** Keeps controllers small, lets the same logic be reused (Race Lab and controllers call the same `RideService`), and makes concurrency tests possible without HTTP.

**Q: Why ViewModels instead of passing entities to views?** Views get exactly what they need (no over-posting, no leaking `PasswordHash`), and form shape ≠ database shape.

**Q: How does a request flow?** Routing → authentication (cookie → `User`) → **rate-limit middleware** → authorization → antiforgery filter → controller action → service → view → HTML. PRG pattern: POST actions redirect to GET, so refresh doesn't resubmit (and the idempotency key covers the case where it does).

**Q: Security basics here?** BCrypt password hashing, HttpOnly auth cookie, global antiforgery (CSRF) validation on all POSTs, `[Authorize(Roles=…)]`, Razor auto-encodes output (XSS), EF parameterises queries (SQL injection), login rate-limited.

**Q: Dependency injection lifetimes?** `AppDb`, `RideService`, `WalletService` = scoped (per request; DbContext isn't thread-safe). `IConnectionMultiplexer`, `RedisLock`, limiter, cache = singleton (stateless/thread-safe by design). `LabService` is singleton and creates scopes manually to simulate separate requests.

## Core concepts
- **Race condition**: outcome depends on timing. Shapes: *check-then-act* (`if pending → set accepted`) and *read-modify-write* (`balance = balance - x`).
- **Concurrency vs parallelism**: concurrency = many tasks in progress; parallelism = simultaneous on multiple cores. Races occur in both.
- **Critical section / mutual exclusion**, **atomicity** (indivisible: one SQL statement, Redis Lua, `SET NX`), **lost update**, **idempotency**, **deadlock** (avoided by consistent lock order: ride lock → driver lock).
- **Pessimistic** (lock first; Redis lock) vs **optimistic** (detect conflict at write via `rowversion`; EF `[Timestamp]`).

## What I built, and why
1. **Accept ride** — Redis lock `lock:ride:{id}` (`SET NX PX` + token-checked Lua release) serialises competing drivers across *all* web servers (C# `lock` is per-process). Then the EF optimistic check using a SQL Server `rowversion` column (`[Timestamp]`, bumped automatically by the DB on every update): `UPDATE … WHERE Id=@id AND RowVersion=@old`; the loser updates 0 rows → `DbUpdateConcurrencyException` → "already taken". Lock = ordering/efficiency, DB = correctness (**defence in depth**).
2. **Wallet** — `UPDATE Users SET Balance = Balance - @x WHERE Id=@id AND Balance >= @x`: guard + arithmetic in one statement. Money as `long` cents.
3. **Rate limiter** — sorted set per client, Lua: trim old → count → add-or-reject, atomically; time from Redis `TIME`; 429 + `Retry-After`; per-route policies; fails open if Redis down.
4. **Cache-aside** for the driver's available-rides list (3 s TTL + invalidation on request/accept/status). Redis errors fall through to DB.
5. **Idempotency** — hidden form key claimed with `SET NX` + per-rider lock → double-click or two tabs create one ride.

## Likely questions
**Why a Lua script?** Redis runs it as one uninterruptible unit; "count then add" can't interleave with other clients. Without it the limiter itself has a race.
**Why `SET NX PX`, not `SETNX` + `EXPIRE`?** Two commands aren't atomic; a crash between them leaves a lock that never expires.
**Why a random token + Lua release?** If my TTL expires while I'm slow, someone else holds the lock; plain `DEL` would free *their* lock. (Test: `Expired_lock_holder_cannot_release_the_new_owners_lock`.)
**TTL expires mid-operation?** Two actors could enter the critical section — exactly why the DB version check exists. Could add a watchdog/lock extension or fencing tokens.
**Is it Redlock?** No: single-node. Replication failover can lose a lock; Redlock (majority of N nodes) helps, though fencing tokens/DB checks are still the real correctness backstop (Kleppmann's critique).
**Fixed vs sliding window vs token bucket?** Fixed: cheap, allows 2× burst at the boundary. Sliding log (mine): exact, O(limit) memory. Token bucket: allows bursts, O(1) memory. Leaky bucket: constant drain rate.
**Who is a "client"?** User id if logged in, else IP. Behind a proxy use `ForwardedHeaders`, or everyone looks like the proxy.
**Why not built-in `AddRateLimiter`?** It's per-process memory: 3 servers = 3× limit. Redis gives one shared counter.
**Redis down?** Limiter and cache fail open (log + fall through). Lock acquisition throws → the accept fails (fail closed; correctness-critical). Gap: could fall back to DB-only optimistic check.
**Why SQL Server?** Mature relational DB with first-class EF Core support, real row-level locking and transactions, and a built-in `rowversion` type that is ideal for optimistic concurrency. Runs in Docker for the demo. (Alternative: PostgreSQL with `xmin` / `SKIP LOCKED`; with EF it is mostly a provider swap.)
**Why `ExecuteUpdate` for money?** Tracked entities = read-modify-write in app memory (race). `ExecuteUpdate` pushes the guard and arithmetic into one SQL statement.
**Pessimistic or optimistic?** Both: pessimistic where contention is high (accept), optimistic as safety net.
**How did you test concurrency?** A `TaskCompletionSource` gate releases N tasks at the same instant, each in its own DI scope, against real Redis + SQL Server. Asserts exactly-one-winner, no overdraft, money conserved. A negative control (buggy twin) proves the harness *can* detect the race (30 winners). Caveat: concurrency tests are probabilistic; `Task.Delay` in the buggy path widens the window.
**Improvements?** Redlock/fencing tokens, SignalR for live updates, Redis GEO for nearest-driver matching, PostgreSQL, token bucket, metrics (429s, lock contention), real payments.

## Questions about the Uber-style features
**Why Redis GEO for drivers and not SQL?** Position changes every ~1.5 s per driver and is disposable. Redis keeps it in memory (`GEOADD`, `GEOSEARCH` radius query in O(log N)); SQL would take constant write load for data that is stale in 30 s anyway. A companion sorted set holds the last heartbeat; a driver silent for 30 s is "offline" automatically (no cleanup job).
**How does surge pricing work?** `ratio = (pendingRequests + 1) / max(1, onlineDrivers)`; multiplier = `1 + 0.25·(ratio − 1)`, capped at 2×. Supply comes from Redis presence, demand from SQL. The fare is **always computed on the server** from coordinates — a rider can't tamper with the price from the browser.
**How is realtime done, and how does it scale?** SignalR over WebSockets. With several web servers a client connects to only one, so I use the **Redis backplane**: every server publishes/subscribes on Redis pub/sub, so an event raised on server A reaches clients on server B. The events carry no data (`Refresh`); the browser re-fetches the server-rendered panel — one source of truth, and no stale client state.
**Why is the service independent of SignalR?** `IRideNotifier` interface: business code says "ride changed"; the SignalR implementation is swapped in via DI (tests/other transports possible).
**Another race condition in the app?** Rating: "has this ride been rated?" read-then-write lets a double-click rate twice and double-count the driver's average. Fixed with one `UPDATE … WHERE DriverRating IS NULL`; the aggregate is only changed if exactly one row was updated (test fires 10 parallel ratings → 1 succeeds).
**Idempotency key — where does it come from?** The booking form renders a random key; resubmits (double-click, retry) send the same key; Redis `SET NX` claims it and later calls return the original ride.
**Security of the new endpoints?** Hub requires the auth cookie; only Drivers may report location; a location relay to a rider is only sent if the DB confirms *this driver* owns *that ride*; coordinates are range-checked; trips over 150 km or under 200 m are rejected; antiforgery on all POSTs (the fetch calls submit the form token).

**Why is "complete the ride + pay the driver" one transaction?** If the status update committed and the app crashed before the payout, the ride would be "completed" but the driver unpaid. Both writes now share one SQL transaction (`WalletService` joins the caller's transaction via `CurrentTransaction` instead of opening its own), so they commit or roll back together. The status change is also behind the Redis lock plus a valid-transition check, so a double-tapped *Complete* pays exactly once (test fires 10 in parallel).
**Is crediting the driver on cash trips correct?** In this demo the driver's wallet is an *earnings ledger*: every completed trip credits the fare, whether the rider paid by wallet (money moves rider → driver) or cash (rider wallet untouched). In a real product a cash trip would credit the driver minus the platform's commission, or debit the driver for the commission owed — a good "what would you improve" answer.

## Demo script (2 min)
1. Run the app → **Race Lab** → Scenario 1 (30 drivers): red card shows ~30 winners, green shows 1. Scenario 2: unsafe ends at 70.00 with 10 withdrawals, safe ends at 10.00 with 3.
2. Rapidly refresh `/Account/Login` 11 times → `429 Too many requests`. `redis-cli keys 'rl:*'` and `redis-cli zrange 'rl:auth:ip:::1' 0 -1 withscores`.
3. Two browsers: driver **Go online + Simulate driving**; rider books on the map; driver accepts instantly; rider watches the car move; driver completes; rider rates. `redis-cli geosearch drivers:geo fromlonlat 90.40 23.78 byradius 10 km` shows presence.
4. Open `RedisLock.cs`, `RedisRateLimiter.cs` (Lua) and `RideService.AcceptAsync`; walk through them.

## Resume bullets (pick 3–4)
- Built an Uber-style ride-hailing web app with **ASP.NET Core 10 MVC**, **SQL Server**, **Redis** and **SignalR**: map booking, surge pricing, live driver tracking, ratings, wallet.
- Matched rides to nearby drivers with **Redis GEO** presence and pushed realtime updates over SignalR with a Redis backplane for multi-server scale-out.
- Eliminated double-assignment **race conditions** with a Redis **distributed lock** (`SET NX PX` + Lua safe release) layered with EF **optimistic concurrency**; verified by 30-way parallel tests (1 winner vs 30 on the unsafe baseline) and a live "Race Lab" page.
- Implemented an atomic Redis Lua **sliding-window rate limiter** (per-user/IP, per-route policies, `429` + `Retry-After`) shared across instances.
- Prevented wallet double-spend with single-statement conditional updates; added **idempotency keys** and Redis **cache-aside** for hot reads.
