# Perf Improver Memory — YuliiaKovalova/testfx

Last updated: 2026-06-13 UTC

## Build / Test / Perf Commands

- **Build (Debug):** `./build.sh --restore --build -c Debug`
- **Build (Release):** `./build.sh -c Release`
- **Test (unit):** `./build.sh --test -c Debug`
- **Pack + integration tests:** `./build.sh -pack -test -integrationTest`
- **.NET binary:** `.dotnet/dotnet` (build.sh bootstraps; use after restore)
- **Run single test project (filter):**
  `dotnet test test/UnitTests/<Project> -f net8.0 --no-build -c Debug --filter "FullyQualifiedName~<Name>"`
- **Acceptance tests need -pack first** before running
- **Performance runner (Windows only):** `test/Performance/MSTest.Performance.Runner` using PerfView/dotnet-trace

## Task Rotation State

Last run (2026-06-13): Tasks 3, 7
Next run should prioritize: Tasks 1, 4, 5, 6, 7

## Optimization Backlog

| Priority | Area | Opportunity | Notes |
|----------|------|-------------|-------|
| DONE | ConsumeAsync | ✅ Eliminate Array.IndexOf scans (PR branch: perf-assist/eliminate-array-indexof-per-test-result, 2026-06-13) | 3 Array.IndexOf per test → 0; merged into existing switch |
| MED | ServiceProvider | `InternalOnlyExtensions` property creates new `Type[]` on every access; convert to `static readonly HashSet<Type>`. `GetServiceInternal` calls `.FirstOrDefault()` on iterator. | Startup path, not per-test; lower urgency |
| LOW | AsynchronousMessageBus.PublishAsync | `Array.IndexOf(DataTypesProduced, dataType)` per message; small arrays typical | Very low impact for 1-3 element arrays |
| LOW | TypeEnumerator | Duplicate-test dedup path uses LINQ GroupBy+OrderBy; rare path | Startup-only, minimal impact |
| INFO | Build infra | No BenchmarkDotNet project; perf runner is Windows-only (PerfView/dotnet-trace) | Need discussion issue before adding dep |

## Completed Work

### 2026-06-13 — PR: eliminate Array.IndexOf scans in ConsumeAsync hot paths
- **Branch:** `perf-assist/eliminate-array-indexof-per-test-result`
- **Changes:**
  1. `TestApplicationResult.ConsumeAsync`: merged `_failedTestsCount++` and `_totalRanTests++` into the existing switch (eliminated GetType() + 3 Array.IndexOf calls per test)
  2. `AbortForMaxFailedTestsExtension.ConsumeAsync`: replaced `Array.IndexOf(WellKnownTestNode..., testNodeStateProperty.GetType()) != -1` with `testNodeStateProperty is FailedTestNodeStateProperty or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty or CancelledTestNodeStateProperty`
  3. Removed now-unused `using Microsoft.Testing.Platform.Messages` from both files
- **Build:** ✅ Pass | **Tests:** ✅ All unit tests net8.0 + net9.0
- **Status:** Submitted via safeoutputs 2026-06-13

### 2026-06-12 — PR: ServiceProvider GetServiceInternal direct loop + HashSet (UNCONFIRMED)
- Memory says submitted but no PR visible in GitHub — likely safeoutputs patch was created but GitHub PR may not have been opened
- Optimization: InternalOnlyExtensions property → HashSet field; GetServiceInternal direct loop
- Skipping re-attempt until 2026-06-12 PR status confirmed

## Performance Notes

- `private static readonly` fields use PascalCase (SA1311 rule): e.g. `InternalOnlyExtensionTypes`
- `s_camelCase` is used for non-readonly private static fields
- Collection expressions `[...]` compile to `HashSet<T>` when target type is `HashSet<T>`
- The codebase is already well-optimized (PropertyBag linked-list, attribute caching, SearchValues)
- ServiceProvider.GetService is called at test-host startup, not per-test
- Performance runner at `test/Performance/MSTest.Performance.Runner` is Windows-only (PerfView/dotnet-trace)
- `TestNodeUpdateMessage` is in `Microsoft.Testing.Platform.Extensions.Messages` namespace, NOT `Microsoft.Testing.Platform.Messages`

## Monthly Activity Issues

- **2026-06:** Issue created via safeoutputs 2026-06-13 — title: `[perf-improver] Monthly Activity 2026-06`

## Previously Checked-Off Items by Maintainer

(none yet)

## Backlog Cursor

Issues scanned for performance label: zero found (2026-06-13)
Next issues cursor: re-scan each run (small repo)
