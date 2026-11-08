# IGES Hotel and Suites — developer guide

This is an ASP.NET Core 9 / EF Core 9 **backend API** for one brand with independent branches.
The public profile uses the owner-supplied details: IGES Hotel and Suites, Warri, Delta State,
Nigeria, rating 4.5. Real addresses, prices, photographs and menus are entered by administrators.
The API does not invent branch records or automatically create every number in a room range.

## Architecture

The customer website and administration dashboard are in `../Frontend`. See `../Frontend/ADMIN.md` for `/admin` setup, the `AdministrationWorkspace` migration, administrator provisioning, recorded guest stays, website settings and messaging. Outgoing mail requires server-side SMTP settings; incoming mailbox ingestion is not connected.

| Project | Responsibility |
| --- | --- |
| Domain | Entities, hotel brand and room numbering rules |
| Application | Business services, DTOs, validation, repository/storage interfaces and pagination |
| Infrastructure | SQL Server DbContext, Identity store, entity mappings, repository, migrations and image storage |
| api | HTTP routes, authentication, branch authorization and dependency injection |
| Tests | HTTP regression tests using SQLite, room rules and SQL Server model checks |

Application depends on `IHotelRepository`, never on the concrete DbContext or Infrastructure.
The repository also acts as a scoped unit of work. Application uses EF Core's provider-neutral
async LINQ extensions; SQL Server-specific locks, transactions and retries live in Infrastructure.
Domain and Common have no EF dependency. Existing project names remain to keep the refactor focused.

## Setup and migrations

Install the SDK specified in global.json. Configure SQL Server using the existing connection key
`ConnectionStrings__NoName_HotelDbConnectionString`. The existing database name is intentionally
preserved so existing data remains accessible.

```powershell
dotnet restore NoName_Hotel.sol.sln
dotnet build NoName_Hotel.sol.sln
$igesJwtKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
@{ 'JwtSettings:key' = $igesJwtKey } | ConvertTo-Json | dotnet user-secrets set --project NoName_Hotel.api
dotnet ef database update --project NoName_Hotel.Infrastructure --startup-project NoName_Hotel.api
dotnet run --project NoName_Hotel.api
```

Use EF CLI 9.x with EF Core 9. Swagger is available at `/swagger` in Development.
No signing key is shipped in appsettings.json; use user secrets or `JwtSettings__key` (at least
32 UTF-8 bytes). Startup does not automatically migrate the database.

All old migrations moved to Infrastructure with their IDs intact. Continue using the existing
database and `__EFMigrationsHistory`. The new `IgesBranchRoomsAndOfferings` migration adds location,
currency/time zone, rooms, photos, booking-room assignments and hospitality listings. It does not
delete existing users, hotels, countries, bookings or administrator assignments. Review the
migration against a backup before applying it to a deployed database.

Existing Hotel records are now **branches**. Administrators must review which old catalogue entries
are actual IGES branches and correct their names/locations. The migration cannot infer this.
Existing bookings retain their dates, status, totals and room counts without invented assignments.
They still consume branch capacity. Add the complete physical room inventory before accepting new
reservations. Booking owners can update legacy reservations with actual room IDs. Old branches
default to NGN and Africa/Lagos; review currency and time zone for branches outside Nigeria before
accepting reservations. Currency cannot change after a branch has bookings.

## Public endpoints

| Endpoint | Purpose |
| --- | --- |
| `GET /api/v1/hotel` | IGES profile and room categories |
| `GET /api/v1/branches?Location=Warri` | Find branches by location/name |
| `GET /api/v1/branches/{hotelId}` | Branch details, country, currency and time zone |
| `GET /api/v1/branches/{hotelId}/rooms` | Available rooms with photos and specifications |
| `GET /api/v1/branches/{hotelId}/rooms/{roomId}` | Available room detail |
| `GET /api/v1/branches/{hotelId}/offerings` | Published meals, pools, outdoor activities, bar and lounge |

These reads do not require login. `Name` is the branch display name; `BrandName` identifies IGES.
Room/offerings lists support `pageNumber` and `pageSize`. Existing `/api/v1/hotels` and unversioned
hotel routes remain compatible aliases. Country endpoints remain available.

Example room search:

```http
GET /api/v1/branches/1/rooms?checkIn=2027-01-10&checkOut=2027-01-12&category=Vip&guests=2
```

Supply both dates together. Without dates, the search uses tonight in the branch's local time.
Check-in is inclusive; check-out is exclusive. Reserved rooms are hidden for overlapping stays,
including from public detail (404). Adjacent check-out/check-in dates are allowed. Draft and disabled
rooms stay hidden. No guest identity or booking records appear in public room responses.

| Category | Floor | Inclusive numbers |
| --- | --- | --- |
| Normal | 1 | 100–200 |
| VIP | 2 | 210–300 |
| VVIP | 3 | 350–400 |
| Platinum | 4, highest | 480–500 |

Numbers in the gaps are rejected. Numbers are unique within a branch and may repeat in another.

Rooms **160–200** are the first-floor twin-bed section: two separate single beds in one bedroom,
within the Normal category. Save them with `bedType: "Twin"` and `maxGuests` of at least 2.
Room responses include `isTwinRoom`; use `isTwinRoom=true` to find these rooms.

Administrators can designate a room as a honeymoon suite by setting `isHoneymoonSuite: true`.
Honeymoon suites must accommodate at least two guests and be in **VVIP (350–400, third floor)**
or **Platinum (480–500, highest floor)**. A branch can offer both price tiers. Ordinary VVIP and
Platinum rooms remain available alongside designated honeymoon suites, and couples can book any
available room at its advertised price. There is no automatic honeymoon surcharge or discount.

```http
GET /api/v1/branches/1/rooms?isTwinRoom=true&guests=2
GET /api/v1/branches/1/rooms?isHoneymoonSuite=true&guests=2
GET /api/v1/branches/1/rooms?isHoneymoonSuite=true&category=Platinum&guests=2
```

These filters also work with stay dates and on public room detail. `isHoneymoonSuite=false` excludes
designated honeymoon suites; `isTwinRoom=false` excludes the twin section. The hotel profile exposes
the twin section and both honeymoon suite tiers. Rooms still require room/bathroom photographs
before publication and disappear from searches when reserved for the selected stay.

The `TwinBedsAndHoneymoonSuites` migration adds the honeymoon designation and database constraints.
Existing rooms 160–200 are updated to `bedType: "Twin"` with capacity of at least two; review their
photographs/specifications against the new layout. Changed configurations are unpublished until
an administrator verifies and republishes them. The migration stops for review if a changed twin
layout has current/upcoming reservations, or if an existing Twin room is outside 160–200.
Existing rooms are not automatically marketed
as honeymoon suites: administrators select the actual suites. Reservations and quoted prices
are preserved. A reserved room's bed layout or honeymoon designation cannot be downgraded.

## Booking

Register/login using `/api/v1/auth/register` and `/api/v1/auth/login`, then use a bearer token:

```http
POST /api/v1/branches/1/bookings
Authorization: Bearer <token>
Content-Type: application/json

{
  "hotelId": 1,
  "checkIn": "2027-01-10",
  "checkOut": "2027-01-12",
  "guests": 2,
  "rooms": 1,
  "roomIds": [5]
}
```

The selected branch must match HotelId. Distinct RoomIds must match Rooms, belong to that branch,
be available and accommodate the guests. Total = sum of selected nightly rates × nights.
Quoted rates are saved per booking-room assignment; editing a reservation retains prices for
rooms kept on it. Newly selected rooms use their current rate. Branch currency applies with no
currency conversion or payment processing. Legacy branch PerNightRate remains for compatibility.

Pending and confirmed bookings reserve rooms. Cancellation releases them. Existing owner
list/update/cancel and administrator list/confirm/cancel operations remain available under the
branch booking route and old hotel aliases. Past dates are rejected using the branch time zone.
SQL Server writes acquire the same branch update lock inside serializable transactions and EF's
retry strategy. Public availability is advisory; reservation creation rechecks it under that lock.

## Administrator room workflow

System administrators create branches. Assigned branch administrators can update their own branch,
rooms and offerings. Existing HotelAdmin assignments enforce this scope. Public registration only
creates ordinary users; provision privileged accounts/assignments through a trusted admin process.

1. Create the branch with actual Name, Address, CountryId, City, State, CurrencyCode and TimeZoneId.
   Rating defaults to 4.5. A branch can begin with TotalRooms = 0 and legacy PerNightRate = 0;
   actual room prices are entered on the rooms themselves.
2. `POST /api/v1/branches/{hotelId}/rooms` creates a draft:

```json
{
  "number": 210,
  "description": "Describe the actual room and bathroom",
  "bedType": "King",
  "amenities": "List actual amenities",
  "maxGuests": 2,
  "sizeSquareMetres": 30,
  "perNightRate": 50000,
  "isPublished": false,
  "isOperational": true
}
```

3. Upload multipart form data to `POST /api/v1/branches/{hotelId}/rooms/{roomId}/photos`:
   `file` = image, `kind` = 1 (room interior) or 2 (bathroom), `caption` = optional text.
   Upload at least one of each kind. JPEG, PNG and WebP signatures are accepted; maximum 5 MB
   per image and 20 photos per room. Server-generated filenames ignore client-supplied paths.
4. `PUT /api/v1/branches/{hotelId}/rooms/{roomId}` with complete details and IsPublished = true
   publishes the room after photo validation.
5. `GET /api/v1/branches/{hotelId}/admin/rooms` includes drafts and reserved rooms. Unpublish a
   room to remove its listing. Rooms with upcoming reservations cannot be disabled, renumbered
   or have guest capacity reduced. Room management synchronizes TotalRooms; once inventory exists,
   branch updates must match that count.

Delete a photo with `DELETE /api/v1/branches/{hotelId}/rooms/{roomId}/photos/{photoId}`. Before
removing the last image of either kind from a published room, upload a replacement or unpublish it.
Files are stored in `NoName_Hotel.api/wwwroot/uploads`, served publicly and excluded from Git.
Persist/back up that directory for deployment. Replace `IPhotoStorage` with shared object storage
for multiple instances. Deleting a photo record leaves its file for an operational retention/
cleanup policy, avoiding unsafe file deletion inside database retries.

## Kitchen and hospitality listings

Public search: `/api/v1/branches/{hotelId}/offerings?kind=Meal&search=soup`.
Categories: Meal (1), Pool (2), OutdoorActivity (3), Bar (4), Lounge (5).
Search matches names and descriptions. Only published listings appear publicly.

Administrators POST `/offerings`, PUT `/offerings/{offeringId}` and DELETE `/offerings/{offeringId}`
under their branch. Example body (replace sample values with the actual menu):

```json
{
  "kind": 1,
  "name": "Actual meal name",
  "description": "Description supplied by the kitchen",
  "openingHours": "Actual service hours",
  "price": 5000,
  "imageUrl": "",
  "isPublished": true
}
```

POST `/api/v1/branches/{hotelId}/images` with multipart `file` to get an image URL for a listing.
HTTPS URLs are also accepted. GET `/admin/offerings` under the branch includes drafts.
These endpoints support discovery; food ordering and pool/activity ticket reservations are not included.

## Verification and operations

```powershell
dotnet test NoName_Hotel.sol.sln
dotnet ef migrations has-pending-model-changes --project NoName_Hotel.Infrastructure --startup-project NoName_Hotel.api
dotnet ef migrations script --idempotent --project NoName_Hotel.Infrastructure --startup-project NoName_Hotel.api
```

Tests use isolated SQLite databases and inspect SQL Server mappings separately. Real SQL Server
integration is still needed for migration execution on existing data, lock contention, deadlock
retries and rowversion conflicts. No migration has been applied automatically.

Failed logins lock accounts for 15 minutes after 5 attempts; authentication endpoints have an IP
rate limit. `/health/live` and `/health/ready` retain the existing `/healths` aliases. Unexpected
exceptions return sanitized ProblemDetails with a trace ID. Country output caching remains
process-local; room availability is not cached.
