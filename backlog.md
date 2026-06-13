# Efficiency Improver — Backlog & Notes

## Completed Work

### Run 1 (2026-06-05)
- Discovered and validated build/test commands
- Scanned codebase for opportunities; populated initial backlog

### Run 2 (2026-06-08)
- **DONE**: `TrxTestResultExtractor.cs` — replaced 9 PropertyBag walks (7× SingleOrDefault + 2× OfType) with single GetStructEnumerator() pass. PR submitted.

### Run 3 (2026-06-12)
- **DONE**: `NamedPipeBase.cs` — replaced `new byte[4]` heap allocation on every IPC message with `stackalloc byte[4]`. PR submitted.

### Run 4 (2026-06-13)
- **DONE**: `HtmlReport/TestResultCapture.cs` and `JUnitReport/TestResultCapture.cs` — replaced 5 PropertyBag walks (4× SingleOrDefault<T>() + 1× foreach/GetEnumerator()) with single GetStructEnumerator() pass. Removed GetClassAndMethodName() helpers in both files. All unit tests pass (55+22 passed, 4 skipped Windows-only). PR submitted: branch `efficiency/single-pass-propertybag-htmljunit`.

---

## Pending Opportunities

| Priority | File | Description | Metric |
|----------|------|-------------|--------|
| MEDIUM | `AnsiTerminalTestProgressFrame.cs` | `StringBuilder` churn in `Render()` on every progress tick; could cache/reuse buffer | CPU, alloc |
| MEDIUM | `FormatterUtilities.cs` | Repeated `string.Format` allocations in terminal output path | alloc |
| MEDIUM | `DiscoveredTestsJsonSerializer.cs` | Potential streaming JSON allocation patterns | alloc, CPU |
| LOW | `ExceptionFlattener.cs` | LINQ SelectMany/ToList in exception flattening | alloc |
| LOW | Report generators | Synchronous file I/O could be async | I/O wait |

---

## Codebase Notes

- `PropertyBag` is a linked list; `SingleOrDefault<T>()` = O(n) walk. There is a fast-path for `TestNodeStateProperty` via `_testNodeStateProperty` field (O(1)). Use `GetStructEnumerator()` (internal) for non-boxing single-pass collection of multiple property types.
- `InternalsVisibleTo` in `Microsoft.Testing.Platform.csproj` grants access to `PropertyBag.GetStructEnumerator()` and `PropertyBagEnumerator` for: HtmlReport, JUnitReport, TrxReport, VSTestBridge, and several others.
- SDK 11.0.100-preview.5.x required; downloaded by `./build.sh` into `.dotnet/` on first run (~4min, 200MB). System dotnet is 8/9/10.x only.
- Unit tests for extensions live in `test/UnitTests/Microsoft.Testing.Extensions.UnitTests/`; run with `--filter HtmlReport` or `--filter JUnit` via `dotnet test`.
- No public API surface changes needed for any of the completed optimisations — all changed methods are `internal`/`private`.

---

## Backlog Cursor

`after_fourth_scan` — next run should resume from MEDIUM priority items (AnsiTerminalTestProgressFrame, FormatterUtilities).
