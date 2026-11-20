# Adventure King — local game server (PC test environment)

ASP.NET Core 8 minimal API + SQLite. Server-authoritative: the Unity client never decides currency,
items, names or purchases. Runs on the owner's PC only (`http://127.0.0.1:5080`, loopback); nothing is
rented or exposed to the internet. The same code can later run on a VPS (switch `Url`, put it behind
HTTPS/reverse proxy, move the rate limiter to shared storage).

## Run

```
dotnet run --project Server/AdventureKing.Server
dotnet test Server/AdventureKing.Server.Tests
```

Phone testing on the home Wi-Fi: `dotnet run --project Server/AdventureKing.Server --environment Lan`
(listens on `0.0.0.0:5080`, plain HTTP for development builds only) — see `Docs/MOBILE_LAN_TESTING.md`.

World: maps `village_wind_bell` (Thị trấn Chuông Gió) and `meadow_breeze` (Đồng Cỏ Gió). Position saves never
change map; only `POST /characters/{id}/travel/{portalId}` does, after checking the portal is on the character's
current map, within reach, and not used again within 1.5 s (double-submit / spam guard).

Database: `Server/AdventureKing.Server/data/adventureking.db` (git-ignored, WAL mode, schema version in
`PRAGMA user_version`).

## Backup / restore

```
dotnet run --project Server/AdventureKing.Server -- --backup              # online, consistent copy in data/backups
dotnet run --project Server/AdventureKing.Server -- --verify <file>       # integrity_check + schema check
dotnet run --project Server/AdventureKing.Server -- --restore <file>      # server stopped; keeps *.pre-restore copy
```

Restore is covered by `BackupRestoreTests` (backup → data loss → restore → exact state back; corrupt file refused).
Copy `data/backups` to an external drive/cloud regularly — a backup on the same disk is not a disaster backup.

## What is real today

| Area | Status |
|---|---|
| Register / login (username + password) | Real: PBKDF2-SHA256 210k, constant-time verify, hashed bearer tokens (14 days), logout, rate limit |
| Google / Facebook | **Not available** — endpoints return 501 and never issue a session. Needs the owner's OAuth client IDs + server-side ID-token verification |
| Characters | 4 slots, server name rules (Vietnamese, 3–16, homoglyph/leet/impersonation/profanity filter, unique), ownership re-checked on enter |
| Tutorial progress | Saved per character, strictly sequential, never resets |
| Wallet | Gold per character, Diamonds per account; append-only ledger with idempotency keys; balances can't go negative |
| Diamond store | Exact approved catalog; tags computed from numbers. **Real payments disabled**: `/store/purchase` always returns 403 `payments_disabled`. `GrantVerifiedPurchase` (receipt-unique, price-checked, first-purchase bonus, once-only starter, month card 15 + 3/day × 30 = 105 on Vietnam days) is only callable by a future approved payment verifier and by tests |
| Starter pack | Not sellable until the owner configures its rare item |
| VIP | Computed from verified paid VND only; thresholds empty until the owner sets `VipThresholdsVnd` (10 increasing values) |

## API (v1)

`POST /auth/register|login|logout`, `POST /auth/google|facebook` (501), `GET /profile`, `POST /characters`,
`POST /characters/name-check`, `POST /characters/{id}/enter`, `POST /characters/{id}/tutorial/{step}`,
`GET /store/catalog`, `POST /store/purchase/{id}` (403), `GET /store/history`, `GET /store/month-card`,
`POST /store/month-card/claim`, `GET /vip`, `GET /health`. Errors: `{ "code", "message" }` with an HTTP status.
