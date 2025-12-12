# DBManager Project History Log

## 2025-12-12 — System.Data.SqlClient -> Microsoft.Data.SqlClient migration (in progress)

Changes applied:
- Added `Microsoft.Data.SqlClient` PackageReference to `SoftwareCenter.Module.DBManager.csproj`.
- Implemented `Services/SqlClientAdapter.cs` to centralize usages of `Microsoft.Data.SqlClient` and provide a single injection point for connection strings.
- Added `DBManagerModule` registration to inject `SqlClientAdapter` using configuration `ConnectionStrings:DBManager` or `DBManager:ConnectionString`.

Validation performed:
- Solution builds successfully after adapter and csproj change.

Remaining actions:
- Replace any existing direct usages of `System.Data.SqlClient` with `SqlClientAdapter` calls. (Search returned no direct usages in the current workspace.)
- Add integration tests that run against a test SQL Server or dockerized SQL Server to validate transactions, pooling, and connection behavior.
- Remove any lingering `System.Data.SqlClient` package references if found in future.

Notes:
- The adapter is intentionally small and focused on common patterns (ExecuteNonQueryAsync, ExecuteScalarAsync, FillDataTableAsync). If DBManager requires transaction support or advanced features, expand the adapter to expose `BeginTransactionAsync` and a lightweight unit-of-work abstraction.
