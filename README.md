# Vinex Fasteners – Desktop HR Management System

Windows WPF desktop client + ASP.NET Core Web API + SQL Server (EF Core). Roles: HR Administrator (full) and Management (read-only), enforced in the API.

**Status:** Phase 1 (foundation: auth, users, audit, shell) complete. Employees, Attendance, Shifts, Leave and Reports follow, one module at a time.

📘 **Full documentation: [docs/DOCUMENTATION.md](docs/DOCUMENTATION.md)**

## Quick start

```
docker compose up -d                     # local SQL Server
# set Bootstrap__AdminUsername / Bootstrap__AdminPassword (10+ chars) for the first start
dotnet run --project src/Vinex.HR.API
dotnet run --project src/Vinex.HR.Desktop   # Windows only
dotnet test                                 # needs Docker
```

Requires the .NET 10 SDK.
