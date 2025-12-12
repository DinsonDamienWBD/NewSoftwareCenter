# SoftwareCenterRefactored .NET net10.0 Upgrade Tasks

## Overview

This document tracks the execution of the solution-wide upgrade from `net8.0` to `net10.0`. All projects will be updated in a single atomic pass, followed by build and test validation and a final commit.

**Progress**: 1/4 tasks complete (25%) ![0%](https://progress-bar.xyz/25)

Note: Preparatory migration documentation and scaffolding have been added to the repository to support the three migration scenarios. See notes below.

---

## Tasks

### [✓] TASK-001: Verify prerequisites *(Completed: 2025-12-12 05:56)*
**References**: Plan §Phase 0, Plan §7

- [✓] (1) Verify .NET 10 SDK is installed on the execution environment per Plan §Phase 0 (run `dotnet --list-sdks`)
- [✓] (2) Runtime/SDK version meets minimum requirements (**Verify**)
- [✓] (3) If `global.json` exists, verify it references a compatible 10.x SDK or update it per Plan §Phase 0
- [✓] (4) `global.json` updated or verified to reference a 10.x SDK when present (**Verify**)

### [ ] TASK-002: Atomic framework and dependency upgrade with compilation fixes
**References**: Plan §Phase 1, Plan §3, Plan §4, Plan §5, Plan §6

- [ ] (1) Update `<TargetFramework>` to `net10.0` in all projects listed in Plan §4 (append `net10.0` for multi-targeted projects) and update any Directory-level props/targets that lock TargetFramework per Plan §3
- [ ] (2) All project files updated to target `net10.0` (**Verify**)
- [ ] (3) Apply package versions explicitly from Plan §5 (update `PackageReference` and `Directory.Packages.props` entries as required)
- [ ] (4) All package references match versions in Plan §5 or are explicitly updated (**Verify**)
- [ ] (5) Run `dotnet restore` for the solution per Plan §Phase 1
- [ ] (6) All dependencies restored successfully (**Verify**)
- [ ] (7) Build the full solution and fix compilation errors referencing Plan §6 Breaking Changes Catalog (address Kernel source-incompatible APIs and Host behavioral changes as discovered)
- [ ] (8) Solution builds with 0 errors (**Verify**)

**Preparatory work completed**:
- Created `CONTRIBUTING.md` with migration guidelines describing the three scenarios.
- Added a central `JsonOptionsProvider` at `Contract, UI & Routing/Core/Serialization/JsonOptionsProvider.cs` to standardize `System.Text.Json` options across the solution.
- Added `Microsoft.Data.SqlClient` package reference to `Modules/DBManager/SoftwareCenter.Module.DBManager.csproj` to prepare DB driver migration.
- Created PR checklist & branch templates to standardize migration PRs.

**Current blockers**:
- Full solution build is failing due to external environment issues:
  - `apphost.exe` files under `obj/Debug/net10.0` are locked by running processes (MSB4018). Close running apps/IDE instances and retry the build.
  - NuGet restore reported project load failures intermittently; ensure network access to nuget.org and that no custom sources are blocking restore.

---

### [ ] TASK-003: Run full test suite and validate upgrade
**References**: Plan §Phase 2, Plan §7

- [ ] (1) Discover and run all test projects in the repository per Plan §7 (if present)
- [ ] (2) Fix any test failures (reference Plan §6 Breaking Changes Catalog for likely causes)
- [ ] (3) Re-run tests after fixes
- [ ] (4) All tests pass with 0 failures (**Verify**)

### [ ] TASK-004: Final commit
**References**: Plan §10

- [ ] (1) Commit all remaining changes with message: "TASK-004: chore(upgrade): upgrade all projects to net10.0 and apply package updates"

---

If you want me to continue automated code changes, first resolve the `apphost.exe` lock (close running processes) or confirm you want me to proceed creating migration branches and PR patches without running a full solution build.

