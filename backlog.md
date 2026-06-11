# Efficiency Improver Backlog — YuliiaKovalova/testfx

Last updated: 2026-06-11

## Completed

| Date | PR/Branch | Description |
|---|---|---|
| 2026-06-11 | `efficiency/named-pipe-stackalloc-header` | `NamedPipeBase.cs` — replace two `ArrayPool<byte>.Rent(4)`/Return blocks with single 8-byte `stackalloc` + `MemoryStream.Write(Span<byte>)` for IPC message headers |

## Pending

### LOW priority

| # | File | Issue | Measurement strategy |
|---|---|---|---|
| 1 | No benchmark suite | Add IPC microbenchmarks to measure per-message throughput | Enables quantitative energy evidence for future improvements |

### LOW priority / Already reviewed

| # | File | Status |
|---|---|---|
| - | `PropertyBag.cs` | Already optimal — linked list, struct enumerator, no boxing |
| - | `BaseSerializer.cs` | Already optimal — ArrayPool + Span, stackalloc in sync paths |
| - | `TelemetryCollector.cs` | Short-circuits when disabled; `ConcurrentDictionary.AddOrUpdate` is fine for enabled path |
| - | `HumanReadableDurationFormatter` | Has `#if NET8_0_OR_GREATER` fast path already |

## Notes on Codebase

- Preprocessor convention: `#if NET` = net8.0/net9.0; `#if NET8_0_OR_GREATER` for net8+ APIs. NO bare `#if NETCOREAPP`.
- `PropertyBag` uses `GetStructEnumerator()` to avoid boxing — do not add LINQ calls.
- No benchmarks exist in the repo — all measurement is allocation-count proxy or wall-clock timing.
- `NamedPipeBase` now uses `stackalloc` for 8-byte header; `BitConverter.TryWriteBytes(Span, int)` available in `#if NET` (net8.0+).
