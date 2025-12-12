# DBManager SQL Client Migration

This directory contains the `SqlClientAdapter` which centralizes usage of `Microsoft.Data.SqlClient` for DBManager.

Migration steps taken:
- Added `Microsoft.Data.SqlClient` PackageReference to `SoftwareCenter.Module.DBManager.csproj`.
- Implemented `SqlClientAdapter` to encapsulate common operations.

Next steps for complete migration:
- Replace existing direct usages of `System.Data.SqlClient` (if any) with `SqlClientAdapter`.
- Update integration tests to validate transactions and connection behavior.
- Remove any leftover `System.Data.SqlClient` package references.
