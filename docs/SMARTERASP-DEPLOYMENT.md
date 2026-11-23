# SmarterASP deployment

NoNameHotel is a single-site deployment: the ASP.NET Core 10 API serves the built React app
from the same origin. Use the Windows hosting plan (`W60-US`) and create an MSSQL database.
The API uses SQL Server and does not run migrations automatically at startup.

## Prepare the account

1. In the hosting control panel, add a website and note its temporary URL.
2. Create an MSSQL database. Copy its connection string from the database details page.
3. If preserving the existing Azure database, export it as a `.bacpac`, import it into local SQL
   Server, make a `.bak`, then restore that backup to the new SmarterASP database. Check the
   database migration history after restore; do not assume a fresh database if existing hotel,
   user, booking, or photo records need to be retained.
4. Copy `docs/appsettings.SmarterAsp.example.json` to
   `NoName_Hotel.api/appsettings.Production.json`. Put the SmarterASP SQL connection string in
   `ConnectionStrings.NoName_HotelDbConnectionString` and a newly generated random secret of at
   least 32 UTF-8 bytes in `JwtSettings.key`. Generate one in PowerShell with
   `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))`.
   This production file is ignored by Git. Set `PasswordReset.FrontendUrl` to the public site
   origin and configure `HotelMail` with the SMTP host, port, verified sender, username, and
   password. Without both settings, reset requests still return a generic response but no email
   can be delivered.

## Build the deploy package

From the solution directory (`NoName_Hotel.sol`), run:

```powershell
.\scripts\Publish-SmarterAsp.ps1
```

The script builds the React frontend, publishes the API as a framework-dependent .NET 10 app,
copies the frontend into `wwwroot`, creates an idempotent EF migration SQL file, and packages the
site. It keeps `appsettings.Production.json` outside the ZIP. The package folder is under
`artifacts/smarterasp/<timestamp>/`.

## Deploy

1. Upload and extract `NoNameHotel-SmarterASP.zip` into the website root (the same folder as
   `web.config`).
2. Upload the separate `private-config/appsettings.Production.json` into that same root. Do not
   put it under `wwwroot`.
3. If this is a new empty database, apply `database-migrations-idempotent.sql` to it using the
   database tools. If you restored the Azure database, the idempotent script applies only newer
   migrations. Review the SQL and take a database backup before applying it.
4. Browse the temporary URL. Check `/health/live` for app health and `/healths/ready` for database
   connectivity. Swagger is intentionally available only in Development.
5. Keep `wwwroot/uploads` backed up: hotel images are stored on the website filesystem.

SmarterASP documents both FTP and Web Deploy publishing, MSSQL restore from `.bak`, and the
Azure `.bacpac` to local SQL Server to `.bak` migration path. The control panel also assigns a
temporary URL for testing before a custom domain is pointed to the account.
