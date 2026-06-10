# Efficiency Improver Backlog — YuliiaKovalova/testfx

Last updated: 2026-06-10

## Completed

| Date | PR | Description |
|---|---|---|
| 2026-06-10 | `efficiency/treenodefilter-span-match` | Eliminate per-segment substring allocation in TreeNodeFilter on .NET — uses `ReadOnlySpan<char>` + `Regex.IsMatch(span)` under `#if NETCOREAPP` |

## Pending

### MEDIUM priority

| # | File | Issue | Measurement strategy |
|---|---|---|---|
| 1 | `NamedPipeBase.cs` lines 44-130 | `ArrayPool<byte>.Rent(4)` for tiny writes to MemoryStream (always sync) — overhead > benefit for 4-byte ints | Replace with a sync helper using `stackalloc` or inline int-to-byte writes |

### LOW priority / Already reviewed

| # | File | Status |
|---|---|---|
| - | `PropertyBag.cs` | Already optimal — linked list, struct enumerator, no boxing |
| - | `BaseSerializer.cs` | Already optimal — ArrayPool + Span, stackalloc in sync paths |
| - | `TelemetryCollector.cs` | Short-circuits when disabled; `ConcurrentDictionary.AddOrUpdate` is fine for enabled path |
| - | `HumanReadableDurationFormatter` | Has `#if NET8_0_OR_GREATER` fast path already |

## Notes on Codebase

- Preprocessor convention: `#if NETCOREAPP` = net8.0+net9.0; `#if NET8_0_OR_GREATER` for net8+ APIs. NO bare `#if NET`.
- `PropertyBag` uses `GetStructEnumerator()` to avoid boxing — do not add LINQ calls.
- `ValueExpression.Regex` is `Compiled` — `IsMatch(ReadOnlySpan<char>)` is fully supported.
- `TreeNodeFilter` is marked `[Experimental("TPEXP")]` but is widely used in CI.
- No benchmarks exist in the repo — all measurement is allocation-count proxy or wall-clock timing.
