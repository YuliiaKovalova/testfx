# Perf Improver Memory — YuliiaKovalova/testfx

Last updated: 2026-06-11 UTC

## Build / Test / Perf Commands

- **Build (Debug):** `./build.sh --restore --build -c Debug`
- **Build (Release):** `./build.sh -c Release`
- **Test (unit):** `./build.sh --test -c Debug`
- **Pack + integration tests:** `./build.sh -pack -test -integrationTest`
- **.NET binary:** `/usr/share/dotnet/dotnet` (pre-installed; build.sh handles SDK bootstrap)
- **Run single test project (filter):**
  `dotnet run --project test/UnitTests/<Project> -f net8.0 --no-build -c Debug -- --filter "FullyQualifiedName~<Name>"`
- **Acceptance tests need -pack first** before running
- **Performance runner (Windows only):** `test/Performance/MSTest.Performance.Runner` using PerfView/dotnet-trace

## Task Rotation State

Last run (2026-06-11): Tasks 3, 6, 7
Next run should prioritize: Tasks 1, 2, 4, 5, 7

## Optimization Backlog

| Priority | Area | Opportunity | Notes |
|----------|------|-------------|-------|
| HIGH | ServiceProvider | ✅ DONE - array→HashSet (PR submitted 2026-06-11) | Eliminates per-call alloc |
| MED | ServiceProvider.GetServicesInternal | Iterator state machine allocation on every GetService() | Could return span/ReadOnlySpan or use pooling |
| MED | ReflectHelper | Attribute caching already done; check if .GetCustomAttributes with early-exit possible | Need to profile |
| LOW | AsynchronousMessageBus.PublishAsync | Array.IndexOf on small arrays per message; could use HashSet if arrays grow | Low impact for typical 1-3 element arrays |
| LOW | TypeEnumerator | LINQ usage in startup; could use for-loops | Startup-only, minimal impact |
| INFO | Build infra | No BenchmarkDotNet project; perf runner is Windows-only (PerfView/dotnet-trace) | Opportunity for Task 6 |

## Completed Work

### 2026-06-11 — PR: ServiceProvider HashSet (SUBMITTED)
- **Branch:** `perf-assist/service-provider-hashset`
- **Change:** `InternalOnlyExtensions` (property, `Type[]`, allocated on every call) → `InternalOnlyExtensionTypes` (field, `HashSet<Type>`, allocated once)
- **Build:** ✅ Pass | **Tests:** ✅ Pass (26/26, net8.0 + net9.0)
- **PR:** Submitted via safeoutputs 2026-06-11

## Performance Notes

- `private static readonly` fields use PascalCase (SA1311 rule): `MuxerExec`, `TemplateFieldRegex`, etc.
- `s_camelCase` is used for non-readonly private static fields (see RoslynHashCode.cs as exception - Roslyn origin).
- Collection expressions `[...]` compile to `HashSet<T>` when the target type is `HashSet<T>`.
- The codebase is already well-optimized (PropertyBag linked-list, attribute caching, SearchValues).
- ServiceProvider.GetService is called at test-host startup, not per-test.
- Performance runner at `test/Performance/MSTest.Performance.Runner` is Windows-only (PerfView/dotnet-trace).

## Monthly Activity Issues

- **2026-06:** Issue created via safeoutputs (temp id: aw_monthly0611) — track PR once numbers known

## Previously Checked-Off Items by Maintainer

(none yet)

## Backlog Cursor

Issues scanned for performance label: zero found (2026-06-11)
Next issues cursor: re-scan each run (small repo)
