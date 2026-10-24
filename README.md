# NoName Hotel API

ASP.NET Core 9 / EF Core 9 hotel catalogue and booking API, using SQL Server, Identity and JWT.

## Run locally

Install the SDK specified in global.json, restore, and build:

```powershell
dotnet restore NoName_Hotel.sol.sln
dotnet build NoName_Hotel.sol.sln --no-restore
```

The development connection defaults to the NoName_HotelDb LocalDB database. Override it with
the environment variable ConnectionStrings__NoName_HotelDbConnectionString for another SQL Server.
The design-time factory uses this same variable and default database.

Set JWT configuration through environment variables or user secrets. The signing key must contain
at least 32 UTF-8 bytes. Use a new randomly generated key, not the old value in Git history:

```powershell
dotnet user-secrets set "JwtSettings:key" "<new-random-secret>" --project NoName_Hotel.api
```

Issuer, audience and duration are configured in appsettings.json. Production requires a signing key;
startup fails if it is missing or invalid. Development can use a temporary random key, in which case
tokens stop working whenever the app restarts. Secrets and bearer tokens are not logged.
Changing the signing key invalidates previously issued JWTs. Any previously deployed key must also
be replaced in that deployment's secret store; changing source files cannot rotate remote secrets.

## Database migration

Review and apply the generated StabilizeBookingsAndIndexes migration before running the changed API:

```powershell
dotnet ef database update --project NoName_Hotel.Domain --startup-project NoName_Hotel.api
dotnet run --project NoName_Hotel.api
```

Use EF CLI 9.x with this solution. No database migration runs automatically on application startup.
The migration restores unique API keys, adds country/hotel indexes, room counts and a SQL Server
booking rowversion. It checks for overlong names/addresses and duplicate index keys before altering
columns. Resolve any reported existing-data problems explicitly; the migration does not silently
truncate or remove records.

Existing hotels start with TotalRooms = 0, so they cannot take new bookings until an administrator
sets a real room count and a positive PerNightRate through the hotel update endpoint.
Existing bookings receive Rooms = 1. Review these defaults against actual inventory before accepting
reservations. The migration cannot reconstruct historical guest counts or prices that were saved
incorrectly by the previous code.

## API contract

The current URL version is v1:

- /api/v1/auth/register and /api/v1/auth/login
- /api/v1/countries and /api/v1/countries/{id}/hotels
- /api/v1/hotels
- /api/v1/hotels/{hotelId}/bookings

Existing unversioned routes remain as compatibility aliases. Other version URLs do not match a
route. Versioning is URL-based; there is no header-based version negotiation.
Swagger is available at /swagger in Development.

All catalogue and booking endpoints require a JWT. Public registration only creates User accounts;
a supplied privileged role or hotel assignment is rejected. Administrator/Hotel Admin accounts must
be provisioned through a trusted administrative process. The old extra Identity API endpoint group
was removed so it cannot bypass the application's registration and login policies.

Hotel creation/update requires PerNightRate and TotalRooms. Booking creation/update accepts Rooms
(default 1), and the total is nightly rate × nights × rooms. Amounts use the application's single
configured business currency; no currency conversion or payment processing is implemented.
Dates use check-in-inclusive, check-out-exclusive nights, with UTC used to reject past check-in dates.

Both pending and confirmed bookings reserve rooms. Cancelled bookings release them.
Room availability is checked per night across all users. SQL Server inventory-changing operations
use short serializable transactions and a common hotel update lock, inside EF's retry strategy.
Capacity cannot be reduced below existing reservations. Room types, individual room assignments,
payments and notifications are outside this basic room-count inventory model.

PATCH /api/v1/countries/{id} accepts application/json-patch+json, at most 20 operations, on /name and
/shortName only. Supported operations: add, replace, remove and test. The patched DTO is validated
before saving; Id and other properties cannot be patched.

## Operational controls

- Authenticated country GET responses are cached per user and query for 5 minutes.
  Successful country and hotel mutations evict the shared catalogue cache tag.
  The default output-cache store is process-local; multiple application instances need a shared
  store and cross-instance invalidation before horizontal scaling.
- Registration/login: 5 requests per minute per client IP. Global: 100 requests per minute per
  authenticated user, or client IP for anonymous requests. Rejections return 429 with Retry-After.
  Limits are process-local. Configure trusted proxy handling deliberately if deploying behind a proxy.
- Failed logins lock an account for 15 minutes after 5 failures.
- Unexpected errors produce ProblemDetails without internal exception details, with a trace ID.
- JSON console logs record method, path, status, duration and booking events. No request bodies,
  passwords, signing keys, API keys or authorization headers are recorded.
- /health/live checks the process; /health/ready checks database connectivity.
  Health responses do not include connection strings or diagnostic details.

## Verification

```powershell
dotnet test NoName_Hotel.sol.sln --no-restore
dotnet ef migrations has-pending-model-changes --project NoName_Hotel.Domain --startup-project NoName_Hotel.api
```

Tests exercise HTTP endpoints with an isolated SQLite database and check SQL Server mappings/query
generation separately. They do not replace a SQL Server integration run for lock contention,
deadlock retries, rowversion conflicts and applying the migration to existing data.
