# Perf Improver Memory — YuliiaKovalova/testfx

Last updated: 2026-06-17 UTC

## Build / Test / Perf Commands

- **Build (Debug):** `./build.sh --restore --build -c Debug`
- **Build (Release):** `./build.sh -c Release`
- **Test (unit):** `./build.sh --test -c Debug`
- **Pack + integration tests:** `./build.sh -pack -test -integrationTest`
- **.NET binary:** `/usr/share/dotnet/dotnet` (pre-installed; build.sh handles SDK bootstrap)
- **Run single test project (MTP host):**
  `dotnet run --project test/UnitTests/<Project> -f net8.0 --no-build -c Debug -- --treenode-filter "/**/<TestClass>/<TestMethod>"`
- **Acceptance tests need -pack first** before running

## Task Rotation State

Last run (2026-06-17): Tasks 1, 2, 3, 7
Next run should prioritize: Tasks 4, 5, 6, 7

## Optimization Backlog

| Priority | Area | Opportunity | Notes |
|----------|------|-------------|-------|
| HIGH | ServiceProvider | ✅ DONE - array→HashSet (PR #pending) | Eliminates per-call alloc |
| MED | ServiceProvider.GetServicesInternal | Iterator state machine allocation on every GetService() | Could return span/ReadOnlySpan or use pooling |
| MED | ReflectHelper | Attribute caching already done; check if .GetCustomAttributes with early-exit possible | Need to profile |
| LOW | AsynchronousMessageBus.PublishAsync | Array.IndexOf on small arrays per message; could use HashSet if arrays grow | Low impact for typical 1-3 element arrays |
| LOW | TypeEnumerator | LINQ usage in startup; could use for-loops | Startup-only, minimal impact |
| INFO | Build infra | No dedicated benchmark suite found (no BenchmarkDotNet project) | Opportunity for Task 6 |

## Completed Work

### 2026-06-17 — PR: ServiceProvider HashSet
- **Branch:** `perf-assist/service-provider-hashset`
- **Change:** `InternalOnlyExtensions` (property, `Type[]`, allocated on every call) → `InternalOnlyExtensionTypes` (field, `HashSet<Type>`, allocated once)
- **Build:** ✅ Pass | **Tests:** ✅ Pass (net8.0 + net9.0)
- **PR:** Pending creation in this run

## Performance Notes

- `static readonly` fields must be PascalCase (SA1311). `private static` mutable fields use `s_camelCase`.
- Collection expressions `[...]` compile to `HashSet<T>` when the target type is `HashSet<T>`.
- The codebase is already well-optimized (PropertyBag linked-list, attribute caching, SearchValues).
- ServiceProvider.GetService is called at test-host startup, not per-test, so gains are modest but correct.
- No BenchmarkDotNet projects found; could add one (Task 6 opportunity).

## Previously Checked-Off Items by Maintainer

(none yet — first run)

## Backlog Cursor

Issues/PRs scanned: none yet (first run)
Next issues cursor: start from beginning
