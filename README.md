# Tyre Lifecycle Management Backend

ABP-based C# backend for the TyreTrack tyre lifecycle management SaaS.

## Planned stack

- C# / .NET 9
- ABP Framework
- ASP.NET Core
- Entity Framework Core
- SQL Server
- OpenAPI / Swagger
- JWT / OpenIddict authentication

## Solution structure

- `src/TyreLifecycle.Domain.Shared`
- `src/TyreLifecycle.Domain`
- `src/TyreLifecycle.Application.Contracts`
- `src/TyreLifecycle.Application`
- `src/TyreLifecycle.EntityFrameworkCore`
- `src/TyreLifecycle.HttpApi`
- `src/TyreLifecycle.HttpApi.Host`

## Initial domain scope

- Customers
- Vehicles
- Tyres

The backend will later expand to inspections, fitments, bookings, job cards, quotes, inventory, warranty, notifications, fleets, branches and lifecycle operations.

## Connected local application

The frontend runs at http://localhost:5174 and proxies /api to https://localhost:65528.
Swagger: https://localhost:65528/swagger.

The database is TyreLifecycleDb in the dedicated tyrelifecycle-sqlserver container,
exposed only locally at 127.0.0.1,14331, with its own tyrelifecycle_sql_data volume.
The previous copy in the secure client portal container is retained for rollback.
SQL credentials are loaded from ignored appsettings.Local.json files; they are
not included in publish output. The container password is in the ignored
.env.tyrelifecycle-sql file. The standalone Compose file is docker-compose.sqlserver.yml.

From this solution folder:

```powershell
docker compose -f docker-compose.sqlserver.yml up -d
dotnet run --project src/TyreLifecycle.DbMigrator
dotnet run --project src/TyreLifecycle.HttpApi.Host
```

The migrator loads settings from its output directory, so these commands also work
from the solution root. EF migrations include the core tables, staff accounts,
workspace documents, and short-lived customer portal challenges.

In Development only, the API seeds the sample workshop records and five staff
roles once. Use the prefilled development login on the frontend. Passwords are
hashed in SQL; sessions use HttpOnly cookies. Development fixtures are not a
production account-provisioning system.

The current screen models use an authenticated JSON workspace adapter backed by
TlWorkspaceDocuments with version checks. The original typed customer/vehicle/
tyre/inspection APIs remain separate; workspace records are not projected into
those original entity tables. API callers must send X-TyreTrack-Request: 1 for
writes; Swagger adds this header automatically. Browser writes must originate
from a configured frontend origin. Login attempts are rate-limited and repeated
bad staff passwords temporarily lock the account.

Customer workspace responses are filtered to the signed-in customer. Customers
can update their own mileage, request bookings, submit advisory scans and decide
quotations; they cannot change quotation prices or view other customers.
Development displays local one-time codes because no SMS provider is configured.
Production returns 503 for PIN requests until an SMS connector is implemented.
External messaging, telematics and hardware connections still need real providers.
