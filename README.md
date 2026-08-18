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
