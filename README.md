# Ride Sync — ASP.NET Core MVC + Redis

An Uber-style ride-hailing web app — map booking, Bike/Car/XL fares with surge pricing, live driver tracking, ratings, receipts, wallet — built to demonstrate
**concurrency handling, race-condition prevention, rate limiting and Redis** in a realistic domain.

**Stack:** ASP.NET Core 10 MVC (Razor) · EF Core + **SQL Server** · Redis (locks, rate limit, cache, GEO) · **SignalR** (Redis backplane) · Leaflet/OpenStreetMap · cookie auth · xUnit

## Run
```bash
docker compose up -d            # SQL Server (:1433) + Redis (:6379)
dotnet run --urls http://localhost:3000
dotnet test RideSync.Tests      # 34 tests; each test class gets its own throwaway SQL Server database
```
The `RideSync` database is created automatically on first start (`EnsureCreated`). **After a schema change, drop it** so it is recreated:
`docker exec ridesync-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'RideSync_Pass123!' -C -Q "ALTER DATABASE RideSync SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE RideSync;"`
Connection strings are in `appsettings.json` (SQL Server `sa` password comes from `docker-compose.yml`; change both for anything real).

### Try it (2 browser windows)
1. **Driver window** (private/incognito): sign up as *Drive* → open **Drive** → press **Go online** → turn on **🎮 Simulate driving** (no GPS needed).
2. **Rider window**: sign up as *Ride* → search or tap the map for pickup & drop → pick Bike/Car/XL → **Request**.
3. Driver sees the request instantly (SignalR) → **Accept** → rider sees the driver's car moving on the map → driver: *I've arrived → Start trip → Complete*.
4. Rider rates the driver; open **Trips** for the receipt. Pay with **Wallet** (top up first) or cash.
5. Open **🧪 Race Lab** for the live concurrency demo.

## Features
| | |
|---|---|
| Booking | Address search (Nominatim), tap-to-pick, route line (OSRM), server-side fare for Bike / Car / XL in ৳ |
| Pricing | `(base + perKm·km + perMin·min) × surge`; surge = pending requests vs. online drivers (`Services/Pricing.cs`) |
| Matching | Online drivers live in Redis **GEO**; only drivers within 15 km are alerted; drivers only see/accept rides for their own vehicle class |
| Realtime | SignalR hub `/hubs/ride`, Redis backplane; driver location relayed to the rider |
| After the trip | Receipt (fare breakdown), trip history, 1–5★ rating (atomic, once only), driver average rating |
| Money | Wallet top-up / withdraw; ride payment by cash or wallet; **the driver's fare is credited to their wallet on every completed ride** (completion + payout are one DB transaction) |
| Places | Pickup / drop-off dropdowns: current location, recent places, popular Dhaka places, plus live address search |


## Project layout
```
Ride-Sync/
├── Controllers/   thin: bind input → call a service → pick a view/redirect
├── Models/        entities (User, Ride, WalletTransaction) + ViewModels/
├── Views/         Razor: Account, Rides (Rider/Driver), Wallet, Lab, Shared/_Layout
├── Services/      business logic (RideService, WalletService, AccountService, LabService)
│                  + Redis helpers (RedisLock, RedisRateLimiter, RedisCache, Idempotency)
├── Middleware/    RateLimitMiddleware ([RateLimit("policy")] attribute)
├── Data/          AppDb (EF Core DbContext)
├── wwwroot/       css + bootstrap
├── RideSync.Tests/  xUnit tests (real SQL Server + Redis)
├── Program.cs · appsettings.json · RideSync.Web.csproj · docker-compose.yml
└── README.md · VIVA.md
```

## Where each requirement lives
| Topic | File | What it does |
|---|---|---|
| Distributed lock | `Services/RedisLock.cs` | `SET NX PX` + Lua compare-and-delete release |
| Race: two drivers, one ride | `Services/RideService.cs` → `AcceptAsync` | Redis lock (layer 1) + `RowVersion` optimistic check (layer 2) |
| Race: double spend | `Services/WalletService.cs` | one atomic `UPDATE … WHERE Balance >= @amt` |
| Race: rate a ride twice | `RideService.RateAsync` | one `UPDATE … WHERE DriverRating IS NULL`; aggregate bumped only if it changed a row |
| Race: double-click / two tabs | `RideService.RequestAsync` | per-rider lock + idempotency key (hidden form field) |
| Rate limit | `Middleware/RateLimitMiddleware.cs`, `Services/RedisRateLimiter.cs` | Lua sliding window, per user/IP, per-route policy, `429` + `Retry-After` |
| Driver presence / nearby | `Services/DriverPresence.cs` | Redis GEO + heartbeat ZSET (30 s TTL) |
| Realtime backplane | `Program.cs`, `Services/RideNotifier.cs` | SignalR over Redis pub/sub |
| Redis cache | `RideService.AvailableAsync` | cache-aside for the driver's hot "available rides" list, invalidated on writes |
| Proof | `Controllers/LabController.cs`, `tests/` | parallel load on safe vs deliberately buggy code |

Rate limits: `appsettings.json → RateLimits` (login/register 10/min per IP, ride request 5/min, wallet 30/min, default 120/min).

## Known limitations
- Single-node Redis lock (not Redlock); fine for one Redis, not multi-master failover.
- SQL Server runs in Docker with a demo `sa` password; use a secrets store and a least-privilege login in production.
- Maps/geocoding use free public OpenStreetMap services (tile.openstreetmap.org, Nominatim, OSRM demo): fine for a demo, not for production traffic.
- Driver location is simulated or browser GPS; no native app, no push notifications.
- No real payment gateway (wallet top-up is a demo), no promo codes.
- Rate limiter fails open if Redis is down.
