# Efficiency Improver Backlog — YuliiaKovalova/testfx

Last updated: 2026-06-12

## Completed

| Date | PR/Branch | Description |
|---|---|---|
| 2026-06-12 | `efficiency/ipc-stackalloc-header` | `NamedPipeBase.cs` — replace ArrayPool rent/return + async MemoryStream writes with stackalloc + synchronous Write for IPC framing |
| (prior run) | Merged to main | `TrxTestResultExtractor` — single-pass PropertyBag walk |

## Pending

### LOW priority

| # | File | Issue | Measurement strategy |
|---|---|---|---|
| 1 | No benchmark suite | Add IPC microbenchmarks to measure per-message throughput | Enables quantitative energy evidence for future improvements |
| 2 | `ReadNextMessageAsync` header read | `BitConverter.ToInt32(_readBuffer, 0)` could use `ReadOnlySpan<byte>` overload | Minor; non-allocating but same CPU cost |

### LOW priority / Already reviewed

| # | File | Status |
|---|---|---|
| - | `PropertyBag.cs` | Already optimal — linked list, struct enumerator, no boxing |
| - | `BaseSerializer.cs` | Already optimal — ArrayPool + Span, stackalloc in sync paths |
| - | `TelemetryCollector.cs` | Short-circuits when disabled; `ConcurrentDictionary.AddOrUpdate` is fine for enabled path |
| - | `HumanReadableDurationFormatter` | Has `#if NET8_0_OR_GREATER` fast path already |
| - | `LoggerFactory.cs` | Lazy logger creation with dictionary cache — already optimal |
| - | `TcpMessageHandler.cs` | Already uses ArrayPool<char> for read path, ArrayPool<byte> for write path |

## Notes on Codebase

- Preprocessor convention: `#if NET` = net8.0/net9.0; `#if NET8_0_OR_GREATER` for net8+ APIs. NO bare `#if NETCOREAPP`.
- `PropertyBag` uses `GetStructEnumerator()` to avoid boxing — do not add LINQ calls.
- No benchmarks exist in the repo — all measurement is instruction-count proxy or wall-clock timing.
- `NamedPipeBase` now uses stackalloc for 8-byte header and synchronous MemoryStream.Write throughout.
- `MemoryStream.Write(ReadOnlySpan<byte>)` is available in .NET Core 2.1+ but only used in #if NET blocks (net8/9).
- `BitConverter.TryWriteBytes(Span<byte>, int)` always succeeds when dest is at least sizeof(int) bytes.
