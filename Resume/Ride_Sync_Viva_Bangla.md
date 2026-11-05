# Ride Sync — Viva প্রস্তুতি (বাংলায় বিস্তারিত ব্যাখ্যা)

> প্রতিটি resume bullet আলাদা করে ব্যাখ্যা করা হয়েছে: **কী করেছি → কেন দরকার ছিল → কীভাবে কাজ করে → কোন ফাইলে আছে**। শেষে viva-র সম্ভাব্য প্রশ্ন-উত্তর।

---

## ০. প্রজেক্টটা এক নজরে (৩০ সেকেন্ডের পরিচয়)

**Ride Sync** হলো Uber/Pathao-র মতো একটা ride-hailing web app। Rider map-এ pickup আর drop ঠিক করে Bike/Car/XL বেছে ride request করে। কাছের driver-রা সাথে সাথে request দেখে, একজন accept করে, rider live map-এ driver-এর গাড়ি চলতে দেখে। ride শেষে rating, receipt আর wallet payment হয়।

**প্রজেক্টের আসল আকর্ষণ** হলো *concurrency* — একসাথে অনেকে একই কাজ করতে গেলে যেন ভুল না হয়। যেমন ৩০ জন driver একই মুহূর্তে একই ride-এ "Accept" চাপলে **ঠিক একজন** পাবে। আমি এটা live দেখানোর জন্য **Race Lab** নামে একটা পেজ বানিয়েছি।

**Tech stack:**
| জিনিস | ব্যবহার |
|---|---|
| Backend | ASP.NET Core 10 MVC (Razor views) |
| Database | SQL Server + Entity Framework (EF) Core |
| Cache / Lock / GEO / Rate limit | Redis |
| Realtime | SignalR (WebSocket) + Redis backplane |
| Map | Leaflet + OpenStreetMap, রাস্তা (route) OSRM, ঠিকানা খোঁজা Nominatim |
| Auth | Cookie authentication, BCrypt password hash |
| Test | xUnit (আসল SQL Server + আসল Redis দিয়ে) |
| Deploy/Run | Docker Compose |

---

## ১. MVC আর প্রজেক্টের গঠন (আগে এটা বুঝতে হবে)

**MVC = Model + View + Controller**
- **Model**: data আর business rule (`Models/`: User, Ride, WalletTransaction; `Services/`)।
- **View**: Razor `.cshtml` ফাইল — শুধু দেখানোর কাজ (HTML)।
- **Controller**: browser থেকে request ধরে, input যাচাই করে, **service-কে ডাকে**, তারপর কোন view দেখাবে বা কোথায় redirect করবে ঠিক করে।

**আমি আলাদা Service layer রেখেছি কেন?**
- Controller ছোট (thin) থাকে।
- একই logic একাধিক জায়গায় ব্যবহার করা যায় (Controller আর Race Lab দুটোই `RideService` ডাকে)।
- HTTP ছাড়াই service test করা যায় — তাই concurrency test লেখা সম্ভব হয়েছে।
- Service গুলো HTTP চেনে না; তারা `Result<T>` ফেরত দেয় (`Outcome`: Success, NotFound, Conflict, Busy, Forbidden, Invalid)।

**একটা request-এর যাত্রা:**
`Routing → Authentication (cookie থেকে user) → Rate-limit middleware → Authorization → Antiforgery → Controller action → Service → View → HTML`
POST করার পর redirect করি (**PRG pattern: Post-Redirect-Get**), যাতে refresh দিলে form আবার submit না হয়।

**Dependency Injection (DI) lifetime:**
- `AppDb` (DbContext), `RideService`, `WalletService` → **Scoped** (প্রতি request-এ একটা নতুন; DbContext thread-safe নয় বলে)।
- `IConnectionMultiplexer` (Redis), `RedisLock`, rate limiter, cache → **Singleton** (stateless ও thread-safe)।

---

## ২. Resume Bullet ১ — পুরো অ্যাপের পরিচয়

> *Built an Uber-style ride-hailing web app with ASP.NET Core 10 MVC, SQL Server, Redis and SignalR: map booking, surge pricing, live driver tracking, ratings, wallet.*

### কী কী feature আছে?

**(ক) Map Booking**
- Rider ঠিকানা সার্চ করতে পারে (Nominatim), অথবা map-এ ট্যাপ করে pickup/drop দেয়। "Current location", "recent places", "popular Dhaka places" ড্রপডাউনেও আছে।
- রাস্তার line দেখায় OSRM দিয়ে।
- **ভাড়া (fare) সবসময় server-এ হিসাব হয়**, browser থেকে নয় — তাই rider browser-এ দাম বদলাতে পারে না (security)।

**(খ) Surge Pricing** (`Services/Pricing.cs`)
- মূল সূত্র: `fare = (base + perKm × km + perMin × মিনিট) × surge`, সর্বনিম্ন ভাড়া ৪০৳।
- দূরত্ব বের করি **Haversine formula** দিয়ে (দুই lat/lng-এর মধ্যে সরলরেখার দূরত্ব), তারপর `× 1.3 (RoadFactor)` করে আসল রাস্তার দূরত্বের কাছাকাছি আনি।
- **Surge কীভাবে?** `ratio = (pending request + 1) / online driver সংখ্যা`। ratio ≤ 1 হলে surge = 1.0। নইলে `1 + 0.25 × (ratio − 1)`, সর্বোচ্চ **2.0×**।
  - মানে: driver কম, request বেশি → ভাড়া বাড়ে (demand/supply)। Supply আসে Redis (কতজন driver online), demand আসে SQL (কতগুলো pending ride)।
- গাড়ির ধরন: Bike (base 30, km প্রতি 12), Car (50, 18), XL (80, 28)।

**(গ) Live Driver Tracking**
- Driver-এর location বারবার server-এ যায়; server সেটা SignalR দিয়ে rider-কে পাঠায়। Rider map-এ গাড়ি চলতে দেখে।
- GPS না থাকলে **"Simulate driving"** চালু করে demo করা যায়।

**(ঘ) Rating**
- Trip শেষে rider ১–৫★ দেয়। একটা ride-এ **একবারই** rating দেওয়া যায়। Driver-এর গড় rating (average) আপডেট হয়।

**(ঙ) Wallet**
- Top-up, withdraw, ride-এর টাকা wallet বা cash-এ দেওয়া। Ride শেষ হলে driver-এর wallet-এ fare জমা হয়।
- টাকা `long` (cents/পয়সা) হিসেবে রাখি, `double` নয় — কারণ floating-point এ দশমিক ভুল হয়।

### Ride-এর অবস্থা (State machine)
`Pending → Accepted → DriverArrived → InProgress → Completed` (আর যেকোনো আগের ধাপে `Cancelled`)।
শুধু বৈধ transition অনুমোদিত (`RideService.Transitions` ডিকশনারি)। যেমন `Pending` থেকে সরাসরি `Completed` হওয়া যাবে না।

---

## ৩. Resume Bullet ২ — Redis GEO + SignalR + Backplane

> *Matched rides to nearby drivers with Redis GEO presence and pushed realtime updates over SignalR with a Redis backplane for multi-server scale-out.*

### (ক) Redis GEO দিয়ে কাছের driver খোঁজা

**সমস্যা:** Driver প্রতি ~১.৫ সেকেন্ডে location পাঠায়। এটা SQL-এ লিখলে database-এ অনবরত write load পড়ে — অথচ ৩০ সেকেন্ড পরে সেই data অর্থহীন।

**সমাধান:** Online driver-দের position রাখি **Redis GEO** (in-memory)-তে।
- `GEOADD` → driver-এর position সেভ।
- `GEOSEARCH ... BYRADIUS` → নির্দিষ্ট ব্যাসার্ধের (আমার ক্ষেত্রে **১৫ কিমি**) মধ্যে driver খোঁজা, প্রায় O(log N)।
- আলাদা একটা **sorted set**-এ প্রতিটা driver-এর শেষ heartbeat সময় রাখি। **৩০ সেকেন্ড চুপ থাকলে driver অটো "offline"** — কোনো cleanup job লাগে না।
- তাই শুধু ১৫ কিমির মধ্যের driver-রাই request alert পায়, আর driver শুধু নিজের **vehicle class**-এর ride (Bike চালক শুধু Bike ride) দেখে ও accept করতে পারে।

(`Services/DriverPresence.cs`)

### (খ) SignalR দিয়ে Realtime

- **SignalR** = ASP.NET-এর realtime library; WebSocket ব্যবহার করে (না থাকলে fallback)। Server নিজে থেকে browser-কে খবর পাঠাতে পারে — browser বারবার জিজ্ঞেস (polling) করতে হয় না।
- Hub endpoint: `/hubs/ride`।
- Driver-এর location → server → ঐ ride-এর rider। **নিরাপত্তা:** DB দিয়ে নিশ্চিত করি "এই driver-ই এই ride-এর driver" তবেই relay করি। শুধু Driver role location পাঠাতে পারে; coordinate range যাচাই হয়।
- **ডিজাইন সিদ্ধান্ত:** event-এ data পাঠাই না, শুধু `Refresh` সংকেত। Browser তখন server-rendered panel আবার আনে। ফলে সত্যের উৎস (source of truth) একটাই, stale data থাকে না।
- `IRideNotifier` interface: business code শুধু বলে "ride বদলেছে"; আসল SignalR বাস্তবায়ন DI দিয়ে বসে। ফলে service SignalR-এর উপর নির্ভর করে না।

### (গ) Redis Backplane কেন?

**সমস্যা:** App যদি ৩টা server-এ চলে, একজন client শুধু **একটা** server-এর সাথে connected। Server A-তে ঘটা event-এর শ্রোতা যদি Server B-তে থাকে, সে খবর পাবে না।

**সমাধান:** সব server **Redis pub/sub**-এ publish/subscribe করে। Server A event publish করলে Redis সেটা সব server-এ পৌঁছে দেয়, আর প্রতিটা server নিজের connected client-দের জানায়। এভাবে **multi-server scale-out** সম্ভব।

---

## ৪. Resume Bullet ৩ — Race Condition + Distributed Lock

> *Eliminated double-assignment race conditions with a Redis distributed lock (SET NX PX + Lua safe release) layered with EF optimistic concurrency; verified by 30-way parallel tests (1 winner vs 30 on the unsafe baseline) and a live "Race Lab" page.*

### Race condition কী?
দুই বা ততোধিক কাজ একসাথে চললে **ফলাফল নির্ভর করে কে আগে পৌঁছাল তার উপর** — এটাই race condition। দুটো প্রচলিত ধরন:
1. **Check-then-act:** `if (ride pending) { ride = accepted }` — চেক আর লেখার মাঝখানে অন্য কেউ ঢুকে পড়ে।
2. **Read-modify-write:** `balance = balance - x` — পড়ো, হিসাব করো, লেখো; মাঝে অন্যজন একই পুরনো মান পড়ে ফেলে (**lost update**)।

### আমাদের সমস্যা: Double-assignment
৩০ জন driver একসাথে Accept চাপল। সবাই দেখল "ride এখনো Pending" → সবাই নিজেকে driver বসাল → শেষজন জিতল, কিন্তু **৩০ জনকেই বলা হলো সফল**। এটা বাগ।

### সমাধান: দুই স্তর (Defence in depth)

**স্তর ১ — Redis Distributed Lock** (`Services/RedisLock.cs`)
- **Distributed কেন?** C#-এর `lock` শুধু এক process-এর ভেতরে কাজ করে। ৩টা server থাকলে ৩টা আলাদা process — তাই সবার জন্য এক জায়গায় (Redis-এ) lock দরকার।
- **Lock নেওয়া:** `SET lock:ride:{id} {token} NX PX {ttl}`
  - `NX` = key না থাকলেই তৈরি করো (atomic "create if absent")।
  - `PX ttl` = এত মিলিসেকেন্ড পর নিজে মুছে যাবে।
  - `token` = এলোমেলো GUID, যাতে বোঝা যায় lock কার।
- **কেন `SETNX` + `EXPIRE` নয়?** দুটো আলাদা কমান্ড atomic নয়। মাঝখানে crash করলে lock চিরকাল আটকে থাকবে (কখনো expire হবে না)। `SET NX PX` একটাই কমান্ড, তাই নিরাপদ।
- **Lock ছাড়া (release) — Lua script:**
  ```lua
  if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end
  ```
  মানে "value আমার token-এর সমান হলেই মুছো।"
  - **কেন token চেক?** আমি ধীর হলে আমার TTL শেষ হয়ে যায়, অন্য কেউ lock নেয়। তখন সরাসরি `DEL` করলে **তার** lock মুছে যেত। token মিললেই কেবল মুছি।
  - **কেন Lua?** Redis পুরো script একটা অবিভাজ্য (atomic) ধাপে চালায় — "GET করে তারপর DEL" এর মাঝে কেউ ঢুকতে পারে না।
- **Retry:** lock না পেলে জিটারসহ exponential backoff (১০ms থেকে ১০০ms পর্যন্ত) দিয়ে আবার চেষ্টা; সময় শেষ হলে `null` → "Busy, try again"।
- **Deadlock এড়ানো:** দুটো lock লাগে — আগে `ride:{id}`, তারপর `driver:{id}`। সবসময় **একই ক্রমে** নিই, তাই deadlock হয় না। দ্বিতীয় lock-টি এই জন্য যে একজন driver একসাথে দুটো ride নিতে পারবে না।

**স্তর ২ — EF Optimistic Concurrency (`RowVersion`)**
- SQL Server-এর `rowversion` কলাম, EF-এ `[Timestamp]`। প্রতিবার row আপডেট হলে DB নিজে এটা বদলে দেয়।
- EF যে SQL বানায়: `UPDATE Rides SET ... WHERE Id=@id AND RowVersion=@old`
- দুজন একই সময়ে পড়লে দুজনেরই `@old` এক; প্রথমজনের update সফল (RowVersion বদলায়); দ্বিতীয়জনের `WHERE` মেলে না → **০টা row আপডেট** → `DbUpdateConcurrencyException` → "Ride just taken" বলি।

**দুই স্তর কেন?**
- Lock = **ক্রম ও কর্মদক্ষতা** (সবাই লাইনে দাঁড়ায়, DB-তে কম চাপ)।
- DB RowVersion = **শুদ্ধতার চূড়ান্ত গ্যারান্টি**। Lock যদি কোনো কারণে ব্যর্থ হয় (TTL শেষ, Redis failover), DB তবুও ভুল ঠেকাবে।
- এটাই **pessimistic** (আগে lock) + **optimistic** (লেখার সময় সংঘর্ষ ধরা) দুটোর সংমিশ্রণ। যেখানে contention বেশি (Accept) সেখানে pessimistic, নিরাপত্তা জাল হিসেবে optimistic।

### Test কীভাবে করেছি? ("১ জন winner বনাম unsafe-এ ৩০")
- `TaskCompletionSource` দিয়ে একটা "গেট" বানাই। ৩০টা task প্রস্তুত থাকে, গেট খুললেই **একই মুহূর্তে** সবাই চালু হয়। প্রতিটা নিজের আলাদা DI scope-এ (যেন আলাদা request)।
- **Negative control:** ইচ্ছাকৃত বাগওয়ালা `UnsafeAcceptAsync` (পড়ো → ৫০ms ঘুমাও → অন্ধভাবে লেখো) বানিয়েছি। সেটাতে ৩০ জনই "জিতল" — এতে প্রমাণ হয় আমার test আসলে race ধরতে **পারে**। আসল `AcceptAsync`-এ ঠিক **১ জন** জেতে।
- আসল SQL Server + আসল Redis দিয়ে test চলে (mock নয়)।
- **সীমাবদ্ধতা:** concurrency test সম্ভাবনাভিত্তিক (probabilistic); unsafe path-এ `Task.Delay` race-এর জানালা বড় করে দেয়।

### Race Lab পেজ
Browser-এ live দেখায়:
- **Scenario 1:** ৩০ driver একই ride-এ → unsafe: ~৩০ জন winner (লাল), safe: ১ জন (সবুজ)।
- **Scenario 2:** wallet-এ ১০০, ৮০ টাকা করে একসাথে অনেক withdraw → unsafe-এ ব্যালেন্স ভুল (overdraft), safe-এ সঠিক।

---

## ৫. Resume Bullet ৪ — Redis Lua Sliding-Window Rate Limiter

> *Implemented an atomic Redis Lua sliding-window rate limiter (per-user/IP, per-route policies, 429 + Retry-After) shared across instances.*

### Rate limiting কী ও কেন?
একজন client নির্দিষ্ট সময়ে কতবার request পাঠাতে পারবে তার সীমা। কারণ: brute-force login আটকানো, spam/DoS কমানো, server বাঁচানো।

### আমি কীভাবে বানিয়েছি (`Services/RedisRateLimiter.cs`)
- **Sliding window log:** প্রতিটা request একটা **Redis sorted set**-এর সদস্য; তার score = request-এর সময় (ms)।
- প্রতি request-এ একটা **Lua script** চলে এবং এই ৩ ধাপ **atomic** ভাবে করে:
  1. window-এর বাইরের পুরনো entry মোছো (`ZREMRANGEBYSCORE`)।
  2. বর্তমান সংখ্যা গোনো (`ZCARD`)।
  3. `count < limit` হলে নতুন entry যোগ করো (`ZADD`) → অনুমতি; নইলে প্রত্যাখ্যান।
- **Lua কেন?** "গুনে তারপর যোগ করা" যদি atomic না হয়, দুটি request একসাথে `limit-1` গুনে দুজনেই ঢুকে যাবে — rate limiter নিজেই race-এ পড়ত।
- **সময় আসে Redis `TIME` থেকে**, app server থেকে নয় — তাই একাধিক server-এর ঘড়ি আলাদা হলেও গোলমাল হয় না।
- প্রত্যাখ্যান হলে **HTTP 429 Too Many Requests** + **`Retry-After`** header (কত সময় পর আবার চেষ্টা করবে) ফেরত যায়।
- **Per-user/IP:** client চেনার কী = login থাকলে user id, নইলে IP। (Proxy-র পেছনে থাকলে `ForwardedHeaders` লাগবে, না হলে সবাই proxy-র IP দেখায়।)
- **Per-route policy:** `[RateLimit("policy")]` attribute দিয়ে রুট অনুযায়ী আলাদা সীমা (যেমন login কঠোর)। `Middleware/RateLimitMiddleware.cs` এটা প্রয়োগ করে।
- **Shared across instances:** counter Redis-এ থাকায় ৩টা server মিলে একটাই সীমা মানে। (ASP.NET-এর built-in `AddRateLimiter` প্রতি process-এর memory-তে থাকে — ৩ server হলে সীমা ৩ গুণ হয়ে যেত।)
- **Fail open:** Redis ডাউন হলে request আটকাই না (log করে অনুমতি দিই) — সুরক্ষা কমলেও পুরো সাইট বন্ধ হয় না।

### অন্য algorithm-এর সাথে তুলনা
| Algorithm | বৈশিষ্ট্য |
|---|---|
| Fixed window | সহজ, সস্তা; কিন্তু window-এর সীমানায় ২× burst ঢুকতে পারে |
| **Sliding log (আমারটা)** | সঠিক; তবে memory O(limit) |
| Token bucket | burst অনুমতি দেয়, memory O(1) |
| Leaky bucket | ধ্রুব হারে খালি হয় |

---

## ৬. Resume Bullet ৫ — Wallet Double-Spend, Idempotency, Cache-aside

> *Prevented wallet double-spend with single-statement conditional updates; added idempotency keys and Redis cache-aside for hot reads.*

### (ক) Wallet Double-Spend (`Services/WalletService.cs`)
**ভুল পদ্ধতি (race):**
```csharp
user.Balance = user.Balance - 80; SaveChanges();
```
ব্যালেন্স ১০০; দুটো ৮০-টাকার withdraw একসাথে: দুজনই ১০০ পড়ে, ২০ লেখে → মোট **১৬০ টাকা তোলা হলো**, অথচ আছে ১০০ (double spend)।

**আমার সমাধান — একটাই SQL statement:**
```sql
UPDATE Users SET Balance = Balance - 80 WHERE Id = @id AND Balance >= 80
```
- শর্ত চেক (`Balance >= 80`) আর বিয়োগ **একই statement-এ**, তাই DB এটাকে অবিভাজ্য ভাবে চালায়।
- দ্বিতীয়টি এসে দেখে ব্যালেন্স ২০ → শর্ত মেলে না → ০ row আপডেট → "insufficient funds"।
- EF-এ `ExecuteUpdate` দিয়ে করি। (Tracked entity নিলে সেটা আবার app-memory-তে read-modify-write হয়ে যেত।)
- প্রতিটা balance পরিবর্তনের সাথে `WalletTransaction` লেজার এন্ট্রি লেখা হয়।

**Ride complete + driver payout = একটা Transaction:**
Status "Completed" হয়ে গিয়ে payout-এর আগে crash করলে ride শেষ কিন্তু driver টাকা পায়নি — এমন হতো। তাই দুটো একই SQL transaction-এ; হয় দুটোই হবে, নয় কোনোটাই না। `WalletService` caller-এর চলমান transaction-এ যোগ দেয় (`CurrentTransaction`)। Complete বাটন দুবার চাপলেও টাকা **একবারই** যায় (Redis lock + বৈধ-transition চেক)।

### (খ) Idempotency Key (`Services/Idempotency.cs`)
- **Idempotent** = একই কাজ কয়েকবার করলেও ফল একই (একবার করার সমান)।
- **সমস্যা:** Rider "Request" বাটনে ডাবল-ক্লিক বা দুটো ট্যাব, বা দুর্বল নেটওয়ার্কে retry → দুটো ride তৈরি হতে পারে।
- **সমাধান:** Booking form-এ একটা এলোমেলো **hidden key** থাকে। প্রথম request Redis-এ `SET idem:{key} NX` দিয়ে key "দখল" করে; পরের একই key-র request দেখে "আগে থেকেই দখলকৃত" → নতুন ride না বানিয়ে **আগের ride-ই** ফেরত দেয়। সাথে প্রতি-rider lock আছে। Key ২৪ ঘণ্টা পর expire।

### (গ) Cache-aside (`Services/RedisCache.cs`)
- **Pattern:** আগে cache-এ দেখো → থাকলে ওটাই দাও; না থাকলে DB থেকে আনো → cache-এ রাখো → দাও।
- **কোথায়?** Driver-এর "available rides" তালিকা (বারবার পড়া হয়, hot read)। TTL মাত্র **৩ সেকেন্ড**।
- **Invalidation:** ride request/accept/status বদলালে cache মুছে দিই (`InvalidateAsync`), যাতে পুরনো data না দেখায়।
- **Fail-safe:** Redis error হলে request ব্যর্থ হয় না; সরাসরি DB-তে চলে যায়।

### (ঘ) Rating race
"এই ride কি আগে rate হয়েছে?" পড়ে তারপর লিখলে ডাবল-ক্লিকে দুবার rating পড়ত ও গড় ভুল হতো। সমাধান: `UPDATE ... WHERE DriverRating IS NULL`; যে statement ১টা row বদলায় কেবল তখনই driver-এর গড় আপডেট করি। (Test: ১০টা parallel rating → ১টা সফল।)

---

## ৭. নিরাপত্তা (Security) সংক্ষেপে
- **Password:** BCrypt hash (plain text নয়)।
- **Cookie:** `HttpOnly` auth cookie (JavaScript পড়তে পারে না)।
- **CSRF:** সব POST-এ global Antiforgery token যাচাই।
- **Authorization:** `[Authorize(Roles=...)]` — Rider/Driver আলাদা।
- **XSS:** Razor আউটপুট অটো-encode করে।
- **SQL Injection:** EF parameterized query ব্যবহার করে।
- **Login:** rate-limited (brute force ঠেকাতে)।
- **ViewModel:** entity সরাসরি view-তে দিই না → `PasswordHash` ফাঁস বা over-posting হয় না।
- **Business যাচাই:** ১৫০ কিমির বেশি বা ২০০ মিটারের কম trip বাতিল; coordinate range চেক।

---

## ৮. সম্ভাব্য Viva প্রশ্ন ও উত্তর

**প্রশ্ন ১: তোমার প্রজেক্টটা সংক্ষেপে বলো।**
উত্তর: এটা ASP.NET Core MVC দিয়ে বানানো Uber-style ride-hailing app। Map-এ booking, Bike/Car/XL ভাড়া ও surge pricing, Redis GEO দিয়ে কাছের driver খোঁজা, SignalR-এ live tracking, rating ও wallet আছে। আমার ফোকাস ছিল concurrency — একই ride ৩০ driver একসাথে accept করলে ঠিক একজন পাবে — এটা Redis lock + DB optimistic concurrency দিয়ে নিশ্চিত করেছি এবং Race Lab-এ live প্রমাণ করেছি।

**প্রশ্ন ২: Race condition কী? তোমার প্রজেক্টে কোথায় ঘটতে পারত?**
উত্তর: যখন ফলাফল নির্ভর করে একাধিক কাজের সময়ের ক্রমের উপর। আমার প্রজেক্টে ৩ জায়গায়: (১) দুই driver একই ride accept, (২) wallet double-spend, (৩) একই ride দুবার rate করা (এবং double-click-এ দুটো ride তৈরি)।

**প্রশ্ন ৩: Concurrency আর Parallelism-এর পার্থক্য কী?**
উত্তর: Concurrency = অনেক কাজ একসাথে *চলমান* (একটা core-এ ঘুরে ঘুরেও হতে পারে)। Parallelism = সত্যিই একই মুহূর্তে একাধিক core-এ চলা। দুটোতেই race হতে পারে।

**প্রশ্ন ৪: C# `lock` ব্যবহার করলে না কেন? Redis lock কেন?**
উত্তর: `lock` শুধু এক process-এর ভেতরে কাজ করে। একাধিক server থাকলে প্রতিটা আলাদা process, তাই সবার জন্য এক জায়গার (Redis) distributed lock লাগে।

**প্রশ্ন ৫: Lock নিতে `SET NX PX` কেন, `SETNX` + `EXPIRE` কেন নয়?**
উত্তর: দুটো কমান্ড atomic নয়; মাঝে crash হলে lock চিরকালের জন্য আটকে যেত। `SET NX PX` এক কমান্ডে "না থাকলে তৈরি + auto-expire"।

**প্রশ্ন ৬: Release-এ Lua আর random token কেন?**
উত্তর: আমার TTL শেষ হয়ে অন্য কেউ lock নিলে সরাসরি `DEL` করলে তার lock মুছে যেত। তাই "value আমার token হলেই মুছো" — এটা GET+DEL একসাথে atomic করতে Lua লাগে।

**প্রশ্ন ৭: কাজ চলাকালীন lock-এর TTL শেষ হয়ে গেলে কী হবে?**
উত্তর: দুজন critical section-এ ঢুকতে পারে। ঠিক এই কারণেই দ্বিতীয় স্তর (RowVersion optimistic check) রেখেছি — সেটা DB পর্যায়ে ভুল ঠেকায়। উন্নতি: lock extension (watchdog) বা fencing token।

**প্রশ্ন ৮: এটা কি Redlock?**
উত্তর: না, এটা single-node Redis lock। Replication failover-এ lock হারাতে পারে। Redlock (N node-এর majority) কিছুটা সাহায্য করে, তবে আসল নিরাপত্তা DB-র version check/fencing token।

**প্রশ্ন ৯: Pessimistic আর Optimistic concurrency-র পার্থক্য?**
উত্তর: Pessimistic = আগে lock নিয়ে নিই, অন্য কেউ ঢুকতে পারে না। Optimistic = lock ছাড়া এগোই, লেখার সময় version মিলিয়ে সংঘর্ষ ধরি (`rowversion`)। আমি দুটোই ব্যবহার করেছি।

**প্রশ্ন ১০: RowVersion কীভাবে কাজ করে?**
উত্তর: SQL Server-এর `rowversion` কলাম প্রতি update-এ অটো বদলায়। EF `UPDATE ... WHERE Id=@id AND RowVersion=@old` চালায়। কেউ মাঝে বদলে ফেললে ০ row আপডেট হয় ও `DbUpdateConcurrencyException` ছোঁড়ে।

**প্রশ্ন ১১: Deadlock কীভাবে এড়ালে?**
উত্তর: একাধিক lock সবসময় একই ক্রমে নিই (আগে ride lock, পরে driver lock)।

**প্রশ্ন ১২: Rate limiter-এ Lua কেন?**
উত্তর: "trim → count → add" তিন ধাপ atomic না হলে দুই request একসাথে `limit-1` দেখে দুটোই ঢুকে যেত। Redis Lua script অবিভাজ্য ভাবে চালায়।

**প্রশ্ন ১৩: Built-in `AddRateLimiter` কেন ব্যবহার করোনি?**
উত্তর: সেটা প্রতি process-এর memory-তে চলে, ৩ server হলে সীমা ৩ গুণ হয়। Redis-এ একটাই shared counter।

**প্রশ্ন ১৪: Fixed window, sliding window, token bucket-এর পার্থক্য?**
উত্তর: Fixed = সহজ কিন্তু সীমানায় ২× burst। Sliding log (আমার) = সঠিক, memory বেশি। Token bucket = burst অনুমতি, O(1) memory। (বিস্তারিত ৫ নং অংশে টেবিল।)

**প্রশ্ন ১৫: Redis বন্ধ থাকলে কী হবে?**
উত্তর: Rate limiter ও cache **fail open** (log করে চালিয়ে যায়)। কিন্তু lock নেওয়া ব্যর্থ হলে accept ব্যর্থ হয় (**fail closed** — কারণ এটা correctness-critical)। উন্নতি: Redis ডাউনে শুধু DB optimistic check-এ fallback।

**প্রশ্ন ১৬: Wallet double-spend কীভাবে ঠেকালে?**
উত্তর: Read-modify-write বাদ দিয়ে একটাই `UPDATE Users SET Balance = Balance - @x WHERE Id=@id AND Balance >= @x`। শর্ত ও বিয়োগ একসাথে, তাই DB serialize করে; দ্বিতীয়টির ০ row আপডেট → insufficient funds।

**প্রশ্ন ১৭: Idempotency key কী? কেন?**
উত্তর: একই request বারবার এলেও যেন একবারের সমান ফল হয়। Form-এ random key থাকে, Redis `SET NX` দিয়ে প্রথমজন দখল করে, বাকিরা আগের ফলাফল পায়। Double-click/retry-তে একটাই ride তৈরি হয়।

**প্রশ্ন ১৮: Cache-aside কী? কোথায় ব্যবহার করেছ?**
উত্তর: আগে cache দেখো, না পেলে DB থেকে এনে cache-এ রাখো। Driver-এর available-rides তালিকায় (TTL ৩ সেকেন্ড), আর ride বদলালে cache invalidate করি।

**প্রশ্ন ১৯: কাছের driver খোঁজার জন্য SQL না নিয়ে Redis GEO কেন?**
উত্তর: Position প্রতি ~১.৫ সেকেন্ডে বদলায় ও দ্রুত অপ্রাসঙ্গিক হয়ে যায়; SQL-এ এত write অপচয়। Redis in-memory, `GEOSEARCH` radius query দ্রুত, আর heartbeat sorted set দিয়ে ৩০ সেকেন্ডে অটো offline।

**প্রশ্ন ২০: Surge pricing কীভাবে হিসাব হয়?**
উত্তর: `ratio = (pending+1)/onlineDrivers`; ratio ≤ 1 হলে ১.০; নইলে `1 + 0.25×(ratio−1)`, সর্বোচ্চ ২.০×। Fare সবসময় server-এ হিসাব, তাই rider ঠকাতে পারে না।

**প্রশ্ন ২১: SignalR কী? Redis backplane কেন?**
উত্তর: SignalR WebSocket-ভিত্তিক realtime library — server থেকে client-এ push। একাধিক server থাকলে client একটাতেই connected, তাই event সব server-এ পৌঁছাতে Redis pub/sub backplane দরকার।

**প্রশ্ন ২২: WebSocket আর polling-এর পার্থক্য?**
উত্তর: Polling-এ client বারবার জিজ্ঞেস করে (অপচয়, দেরি)। WebSocket-এ একটি স্থায়ী দুমুখী সংযোগ — server নিজে থেকে push করে।

**প্রশ্ন ২৩: Service layer কেন? ViewModel কেন?**
উত্তর: Service: controller ছোট থাকে, logic পুনঃব্যবহারযোগ্য, HTTP ছাড়া test করা যায়। ViewModel: view শুধু যা দরকার তাই পায়, `PasswordHash` ফাঁস বা over-posting হয় না।

**প্রশ্ন ২৪: Scoped আর Singleton-এর পার্থক্য? DbContext Scoped কেন?**
উত্তর: Scoped = প্রতি request-এ একটি instance; Singleton = পুরো অ্যাপে একটি। DbContext thread-safe নয় বলে Scoped। Redis connection thread-safe ও ব্যয়বহুল বলে Singleton।

**প্রশ্ন ২৫: Concurrency কীভাবে test করেছ?**
উত্তর: `TaskCompletionSource` গেট দিয়ে ৩০টি task একই মুহূর্তে ছাড়ি, প্রতিটা আলাদা DI scope-এ, আসল Redis + SQL Server-এ। Assert: ঠিক ১ winner, overdraft নেই, মোট টাকা অপরিবর্তিত। Unsafe বাগওয়ালা সংস্করণে ৩০ winner আসে — প্রমাণ যে test race ধরতে পারে।

**প্রশ্ন ২৬: Ride complete ও driver payout একই transaction কেন?**
উত্তর: আলাদা হলে status Completed হয়ে payout-এর আগে crash করলে driver টাকা পেত না। একই transaction-এ দুটো একসাথে commit/rollback হয়।

**প্রশ্ন ২৭: Cash trip-এ driver-এর wallet-এ টাকা যোগ করা কি ঠিক?**
উত্তর: Demo-তে driver wallet একটি *earnings ledger*। বাস্তবে cash trip-এ platform-এর commission বাদ দিয়ে জমা হবে বা commission driver-এর কাছ থেকে কাটা হবে — এটা আমার উন্নতির পরিকল্পনা।

**প্রশ্ন ২৮: SQL Server কেন?**
উত্তর: পরিণত relational DB, EF Core-এর ভালো সাপোর্ট, সত্যিকারের row-level locking ও transaction, এবং optimistic concurrency-র জন্য উপযুক্ত `rowversion` টাইপ। বিকল্প PostgreSQL (`xmin`)।

**প্রশ্ন ২৯: তোমার প্রজেক্টের সীমাবদ্ধতা/ভবিষ্যৎ উন্নতি কী?**
উত্তর: Redlock/fencing token, lock watchdog, token-bucket limiter, metrics (429 সংখ্যা, lock contention), আসল payment gateway, cash trip-এ commission model, push notification।

**প্রশ্ন ৩০: সবচেয়ে কঠিন সমস্যা কোনটা ছিল?**
উত্তর (প্রস্তাবিত): Race condition ধরা ও প্রমাণ করা। বাগটা সবসময় ঘটে না, তাই গেট-ভিত্তিক parallel test আর negative control (বাগওয়ালা সংস্করণ) বানিয়ে প্রমাণ করেছি যে সমস্যাটা আসল ও সমাধানটা কাজ করে।

---

## ৯. মুখস্থ রাখার মতো মূল সূত্র
- **Lock নেওয়া:** `SET key token NX PX ttl` (atomic, auto-expire)
- **Lock ছাড়া:** Lua — "token মিললে DEL"
- **দুই স্তর:** Redis lock (ক্রম) + RowVersion (শুদ্ধতা)
- **টাকা:** `UPDATE ... SET Balance = Balance - x WHERE Balance >= x` (একটাই statement)
- **Rate limit:** sorted set + Lua (trim → count → add), ফল 429 + Retry-After
- **Realtime:** SignalR + Redis backplane (multi-server)
- **Nearby driver:** Redis GEO (`GEOADD`/`GEOSEARCH`) + heartbeat (৩০ সেকেন্ড)
- **Idempotency:** hidden key + `SET NX`
- **Cache-aside:** cache → miss হলে DB → cache; বদলালে invalidate

## ১০. Demo স্ক্রিপ্ট (২ মিনিট)
1. `docker compose up -d` → `dotnet run --urls http://localhost:3000`
2. **Race Lab** → Scenario 1: লাল কার্ডে ~৩০ winner, সবুজে ১। Scenario 2: wallet-এর পার্থক্য।
3. `/Account/Login` দ্রুত ১১ বার refresh → **429 Too many requests**।
4. দুটো browser: Driver "Go online + Simulate driving"; Rider ride request → Driver accept → rider গাড়ি চলতে দেখে → complete → rating।
5. কোড দেখাও: `RedisLock.cs`, `RedisRateLimiter.cs`, `RideService.AcceptAsync`।
