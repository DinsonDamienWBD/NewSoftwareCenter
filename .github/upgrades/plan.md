# .github/upgrades/plan.md

## Table of contents

- [1. Executive Summary](#executive-summary)
- [2. Migration Strategy](#migration-strategy)
- [3. Detailed Dependency Analysis](#detailed-dependency-analysis)
- [4. Project-by-Project Plans](#project-by-project-plans)
- [5. Package Update Reference](#package-update-reference)
- [6. Breaking Changes Catalog](#breaking-changes-catalog)
- [7. Testing & Validation Strategy](#testing--validation-strategy)
- [8. Risk Management](#risk-management)
- [9. Complexity & Effort Assessment](#complexity--effort-assessment)
- [10. Source Control Strategy](#source-control-strategy)
- [11. Success Criteria](#success-criteria)
- [12. Appendices & References](#appendices--references)

---

## 1. Executive Summary

- Scenario: .NET version upgrade to `net10.0` for the `SoftwareCenterRefactored` solution.
- Scope: All projects in solution (12 projects) will be upgraded simultaneously as a single atomic operation (**All-At-Once Strategy**).

Rationale (summary of assessment):
- 12 projects (small/medium solution) — fits All-At-Once guidance
- All projects currently target `net8.0` (modern .NET) and are SDK-style
- All NuGet packages in assessment reported as compatible (8 packages)
- Low total LOC (4.8k) and very small number of source-incompatible API issues (3) and behavioral changes (3)

Resulting recommendation: Perform a single coordinated upgrade of all projects to `net10.0` in one atomic batch, followed by solution build and test validation.

Known focus areas:
- `SoftwareCenter.Kernel` has 3 source-incompatible API findings — plan to compile and fix these in the same atomic pass
- `SoftwareCenter.Host` has 3 behavioral-change items — pay attention to ASP.NET Core configuration and logging

---

## 2. Migration Strategy

- Selected Strategy: **All-At-Once Strategy** — All projects upgraded simultaneously in a single coordinated operation.

Justification:
- Projects: 12 (<30) and homogeneous (all SDK-style, net8.0) — low fragmentation
- Packages: assessment shows all packages compatible with net10.0
- Code surface: small (≈4.8k LOC) and limited number of API incidents → manageable in a single pass
- Team impact: single atomic pass minimizes multi-targeting complexity and yields a unified codebase targeting net10.0

High-level phases (for planning clarity):
- Phase 0: Preparation (verify .NET 10 SDK, update global.json if present, branch creation)
- Phase 1: Atomic Upgrade (update all project TargetFramework to `net10.0`, update package versions where assessment prescribes, restore, build and fix compilation errors)
- Phase 2: Test Validation (run unit/integration tests, address failures)
- Phase 3: Post-upgrade cleanup (documentation, CI updates, remove temporary flags)

Deliverable for Phase 1: Solution builds with 0 compilation errors.

---

## 3. Detailed Dependency Analysis

Summary:
- The solution dependency graph centers on `SoftwareCenter.Core` (no dependencies, many dependants). `Kernel` depends on `Core`. `Host` depends on `Kernel` and `UIManager`.
- No circular dependencies were reported in the assessment.

Implication for All-At-Once:
- Although dependency ordering is important conceptually (leaf nodes first), the All-At-Once approach upgrades all project TFMs simultaneously so intermediate states are avoided. The execution still must respect imported MSBuild props/targets (Directory.Build.props, Directory.Packages.props) and conditional logic.

Key checks before upgrade:
- Search for `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props` anywhere under repo root and plan to update any TargetFramework or PackageReference entries defined there as part of the atomic change.
- Identify any project files that use conditional TargetFramework logic and ensure the new `net10.0` value satisfies conditions or is added appropriately.

---

## 4. Project-by-Project Plans

Common operations applied to every project (atomic):
1. Update `<TargetFramework>` from `net8.0` to `net10.0` in each `.csproj` (or append to `TargetFrameworks` if the project is multi-targeted per assessment guidance). If a project is multi-targeted, append `net10.0` rather than replacing existing targets.
2. Update package references listed in §5 Package Update Reference (none require version changes according to assessment; review Microsoft.Extensions.* packages which are already at `10.0.1`)
3. Restore dependencies (`dotnet restore`) and build solution to observe compilation errors
4. Fix compilation errors caused by framework/package upgrades (apply changes across projects in the same atomic commit)
5. Rebuild solution and verify 0 errors
6. Run tests (Phase 2)

Project stubs (current → target; complexity/risk):

- `Contract, UI & Routing/Core/SoftwareCenter.Core.csproj`
  - Current: `net8.0` → Target: `net10.0`
  - Packages referenced: `Microsoft.Extensions.DependencyInjection.Abstractions (10.0.1)`, `Microsoft.Extensions.Logging.Abstractions (10.0.1)`
  - Complexity: Low
  - Notes: 1 file with incident — compile and fix if needed

- `Contract, UI & Routing/Kernel/SoftwareCenter.Kernel.csproj`
  - Current: `net8.0` → Target: `net10.0`
  - Packages referenced: `LiteDB (5.0.21)`, `Microsoft.Extensions.DependencyInjection (10.0.1)`, `Microsoft.Extensions.Hosting (10.0.1)`, `Microsoft.Extensions.Logging (10.0.1)`
  - Complexity: Medium (3 source-incompatible API issues reported)
  - Notes: allocate attention to API changes reported (see §6 Breaking Changes Catalog)

- `Contract, UI & Routing/UIManager/SoftwareCenter.UIManager.csproj`
  - Current: `net8.0` → Target: `net10.0`
  - Packages referenced: `HtmlAgilityPack (1.12.4)`
  - Complexity: Low

- `Host/SoftwareCenter.Host.csproj`
  - Current: `net8.0` → Target: `net10.0`
  - Project Kind: AspNetCore
  - Complexity: Medium (3 behavioral changes reported)
  - Notes: review middleware configuration (UseExceptionHandler), logging (AddConsole), and JSON handling

- Modules (AI.Agent, AppManager, CredManager, DBManager, LogNotifManager, SourceManager)
  - All: `net8.0` → `net10.0`
  - Complexity: Low
  - Notes: Module projects reference Core; no package updates required per assessment

---

## 5. Package Update Reference

All packages found in assessment are marked compatible. Include exact versions to preserve reproducible builds.

- `HtmlAgilityPack` — current: `1.12.4` — used by `SoftwareCenter.UIManager`
- `LiteDB` — current: `5.0.21` — used by `SoftwareCenter.Kernel`
- `Microsoft.Extensions.DependencyInjection` — current: `10.0.1` — used by `SoftwareCenter.Kernel`
- `Microsoft.Extensions.DependencyInjection.Abstractions` — current: `10.0.1` — used by `SoftwareCenter.Core`, `SoftwareCenter.Kernel`
- `Microsoft.Extensions.Hosting` — current: `10.0.1` — used by `SoftwareCenter.Kernel`
- `Microsoft.Extensions.Hosting.Abstractions` — current: `10.0.1` — used by `SoftwareCenter.Kernel`
- `Microsoft.Extensions.Logging` — current: `10.0.1` — used by `SoftwareCenter.Kernel`
- `Microsoft.Extensions.Logging.Abstractions` — current: `10.0.1` — used by `SoftwareCenter.Core`, `SoftwareCenter.Kernel`

Action rule: apply versions from assessment explicitly; do not use fuzzy `latest` tokens. If any packages need newer versions due to compilation failures, update them as part of the atomic pass and document the change.

---

## 6. Breaking Changes Catalog

Top items discovered in assessment (recommendation + remediation hints):

- `System.TimeSpan.FromSeconds(double)` and `System.TimeSpan.FromMinutes(double)` — marked source-incompatible in assessment (2 occurrences)
  - Likely cause: code calling overloads with numeric types that became ambiguous; remediation: ensure values are `double` explicitly or use `TimeSpan.FromSeconds((double)x)` / `TimeSpan.FromMinutes((double)x)` or construct via `TimeSpan.FromTicks` if applicable. Compile to discover exact lines and apply minimal type fixes.

- `Microsoft.AspNetCore.Builder.ExceptionHandlerExtensions.UseExceptionHandler(IApplicationBuilder, string)` — behavioral change
  - Remediation: review `Program.cs`/`Startup.cs` exception handling wiring. For minimal changes, ensure middleware registration follows recommended patterns for ASP.NET Core on net10.0; consult ASP.NET Core 10 migration docs if behavior differs.

- `System.Text.Json.JsonSerializer.Deserialize(JsonElement, Type, JsonSerializerOptions)` — behavioral change
  - Remediation: verify code that deserializes from `JsonElement` and pass explicit `JsonSerializerOptions` or use `Get<T>()` helpers where appropriate. Add tests for JSON paths.

- `Microsoft.Extensions.Logging.ConsoleLoggerExtensions.AddConsole(ILoggingBuilder)` — behavioral change
  - Remediation: new logging configuration patterns may require `builder.AddSimpleConsole()` or `AddConsole(options => ...)`. Update logging setup in `Host` and `Kernel` as needed.

Note: The precise code changes will be found after the first build with `net10.0`. Plan to fix all compilation errors during the atomic pass and include the fixes in the same commit.

---

## 7. Testing & Validation Strategy

Per-phase validation checks:

Phase 0 (Preparation)
- Verify .NET 10 SDK is installed. If `global.json` exists, ensure it references a compatible 10.x SDK or update it.

Phase 1 (Atomic Upgrade)
- `dotnet restore` succeeds
- `dotnet build` of the full solution produces 0 compilation errors (warnings allowed but note them)

Phase 2 (Test Validation)
- Discover and run all test projects (if any)
- All unit and integration tests pass

Phase 3 (Post-upgrade)
- Run any end-to-end smoke tests available (if automated)

Validation checklist per project (minimum):
- [ ] Project file TargetFramework set to `net10.0`
- [ ] All referenced package versions match assessment list or are explicitly updated and documented
- [ ] Project builds without errors
- [ ] Unit tests (if present) pass

---

## 8. Risk Management

Top risks:
- Kernel source-incompatible APIs (Medium risk) — mitigation: allocate targeted review and tests, run compilation early in Phase 1 and fix in same atomic pass
- Host behavioral changes in ASP.NET middleware & logging (Medium risk) — mitigation: focus code review on `Program.cs`/`Startup.cs` and logging configuration; add regression tests for request handling and error pages

General mitigations:
- Keep changes atomic and in a single feature branch (`upgrade-to-NET10`)
- Use CI pipeline to run `dotnet build` and tests on the upgrade branch before merging
- Capture and document any package upgrades performed during the pass
- Maintain a rollback plan: if build-break or major regression, revert the single merge commit

---

## 9. Complexity & Effort Assessment

Per-project complexity (relative):
- Low: Core, UIManager, Modules (AI.Agent, AppManager, CredManager, DBManager, LogNotifManager, SourceManager)
- Medium: Kernel (3 source-incompatible issues), Host (3 behavioral changes)

Notes: No high-risk (>10k LOC) projects; overall solution classified as **Simple-Medium** and suitable for All-At-Once.

---

## 10. Source Control Strategy

- Branching: create feature branch `upgrade-to-NET10` from `master` (no pending changes in working tree per assessment). Switch to that branch before applying updates.
- Commit policy (All-At-Once): prefer a single atomic commit containing:
  - All project file TargetFramework changes
  - Directory-level MSBuild imports updates (if any)
  - PackageReference updates
  - Code fixes required to resolve compilation errors

- Commit message template (example):
  - `chore(upgrade): upgrade all projects to net10.0 and apply package updates`

- Pull Request: include link to this `plan.md` and `assessment.md`, list of changed files, verification steps, and CI status. Require at least one reviewer before merge.

---

## 11. Success Criteria

The upgrade is complete when all of the following are true:
- All projects target `net10.0` (no residual `net8.0` values)
- All package updates from §5 applied or explicitly documented
- Solution builds with 0 errors
- All automated tests pass
- No unresolved security vulnerabilities reported

---

## 12. Appendices & References

- Assessment source: `.github/upgrades/assessment.md` (source of metrics and package list)
- Useful commands (for executor):
  - `dotnet --list-sdks` (verify SDKs installed)
  - `dotnet restore` (restore packages)
  - `dotnet build SoftwareCenterRefactored.slnx -c Release` (build solution)

---

<!-- Iteration log
Iteration 1.1: Created skeleton with TOC and placeholders.
Iteration 1.2: Pulled metrics from assessment.md and classified solution as All-At-Once suitable.
Iteration 1.3: Filled Executive Summary, Migration Strategy, Dependency Analysis, Project stubs, Package Reference, Breaking Changes Catalog, Testing, Risk, Complexity, Source Control, and Success Criteria.
Iteration 2.0: Final review and expansion of details in all sections; validate actionability of plan.
Next: Phase 3 - Execution: perform the atomic upgrade to net10.0 and document outcomes.
-->