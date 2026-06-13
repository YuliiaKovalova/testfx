// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;

namespace Microsoft.Testing.Extensions.JUnitReport;

// Projects a TestNodeUpdateMessage into a capped CapturedTestResult so that the
// generator does not retain entire test node payloads (and their potentially huge
// stdout/stderr/stack traces) in memory for the whole session.
internal static class TestResultCapture
{
    internal const int MaxStandardStreamLength = 32 * 1024;
    internal const int MaxStackTraceLength = 32 * 1024;
    internal const int MaxMessageLength = 16 * 1024;
    internal const int MaxIdentityFieldLength = 4 * 1024;
    internal const int MaxTraitFieldLength = 1024;

    // The display name of a test node is also captured for non-terminal (Discovered /
    // InProgress) messages so that the engine can reconstruct the parent chain for
    // every terminal test. This DTO is intentionally tiny because every node in the
    // tree contributes one entry to the parent-chain dictionary.
    internal readonly record struct ParentChainEntry(string DisplayName, string? ParentRawUid);

    public static ParentChainEntry GetParentChainEntry(TestNodeUpdateMessage update)
        => new(
            // Display names are test-controlled and may be very long, so cap them to
            // bound the size of the parent-chain dictionary and the generated XML.
            Truncate(update.TestNode.DisplayName, MaxIdentityFieldLength)!,
            // The parent UID is also test-controlled. Cap it to the same identity
            // budget used everywhere else; lookups against `_parentChain` (also
            // keyed by a truncated UID) stay consistent.
            Truncate(update.ParentTestNodeUid?.Value, MaxIdentityFieldLength));

    public static CapturedTestResult? TryCapture(TestNodeUpdateMessage update)
    {
        TestNode node = update.TestNode;
        TestNodeStateProperty? state = node.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (state is null or DiscoveredTestNodeStateProperty or InProgressTestNodeStateProperty)
        {
            return null;
        }

        string outcome = ClassifyOutcome(state);

        // Single-pass collection of all non-state properties: replaces 4 × SingleOrDefault<T>()
        // + 1 × foreach (5 separate linked-list walks) with one GetStructEnumerator() walk.
        // Also avoids boxing the struct enumerator that the foreach would incur via GetEnumerator().
        TimingProperty? timing = null;
        TestMethodIdentifierProperty? methodId = null;
        StandardOutputProperty? stdoutProp = null;
        StandardErrorProperty? stderrProp = null;
        List<KeyValuePair<string, string>>? traits = null;

        PropertyBag.PropertyBagEnumerator enumerator = node.Properties.GetStructEnumerator();
        while (enumerator.MoveNext())
        {
            switch (enumerator.Current)
            {
                case TimingProperty t: timing = t; break;
                case TestMethodIdentifierProperty m: methodId = m; break;
                case StandardOutputProperty so: stdoutProp = so; break;
                case StandardErrorProperty se: stderrProp = se; break;
                case TestMetadataProperty meta:
                    // Trait keys and values are test-controlled so we truncate them to
                    // bound the size of the in-memory result list and generated XML.
                    traits ??= [];
                    traits.Add(new KeyValuePair<string, string>(
                        Truncate(meta.Key, MaxTraitFieldLength)!,
                        Truncate(meta.Value, MaxTraitFieldLength)!));
                    break;
            }
        }

        TimeSpan duration = timing?.GlobalTiming.Duration ?? TimeSpan.Zero;

        string? className = null;
        string? methodName = null;
        if (methodId is not null)
        {
            className = RoslynString.IsNullOrEmpty(methodId.Namespace)
                ? methodId.TypeName
                : $"{methodId.Namespace}.{methodId.TypeName}";
            methodName = methodId.MethodName;
        }

        string? errorMessage = state.Explanation;
        string? stackTrace = null;
        string? exceptionType = null;
        Exception? exception = state switch
        {
            FailedTestNodeStateProperty f => f.Exception,
            ErrorTestNodeStateProperty e => e.Exception,
            TimeoutTestNodeStateProperty t => t.Exception,
#pragma warning disable CS0618, MTP0001 // CancelledTestNodeStateProperty is obsolete
            CancelledTestNodeStateProperty c => c.Exception,
#pragma warning restore CS0618, MTP0001
            _ => null,
        };

        if (exception is not null)
        {
            errorMessage ??= exception.Message;
            stackTrace = exception.StackTrace;
            exceptionType = exception.GetType().FullName;
        }

        return new CapturedTestResult
        {
            // Identity fields are test-controlled and can be unbounded (e.g. very long
            // UIDs/display names from generated data), so we also cap them to keep the
            // session-wide result list and generated XML within a predictable budget.
            // RawUid and ParentRawUid are used as keys/edges in the parent-chain
            // dictionary, so they must be capped with the same budget on both sides
            // to keep lookups consistent.
            Uid = Truncate(node.Uid.Value, MaxIdentityFieldLength)!,
            RawUid = Truncate(node.Uid.Value, MaxIdentityFieldLength)!,
            ParentRawUid = Truncate(update.ParentTestNodeUid?.Value, MaxIdentityFieldLength),
            DisplayName = Truncate(node.DisplayName, MaxIdentityFieldLength)!,
            Outcome = outcome,
            Duration = duration,
            StartTime = timing?.GlobalTiming.StartTime,
            EndTime = timing?.GlobalTiming.EndTime,
            ClassName = Truncate(className, MaxIdentityFieldLength),
            MethodName = Truncate(methodName, MaxIdentityFieldLength),
            ErrorMessage = Truncate(errorMessage, MaxMessageLength),
            ExceptionType = exceptionType,
            StackTrace = Truncate(stackTrace, MaxStackTraceLength),
            StandardOutput = Truncate(stdoutProp?.StandardOutput, MaxStandardStreamLength),
            StandardError = Truncate(stderrProp?.StandardError, MaxStandardStreamLength),
            Traits = traits,
        };
    }

    private static string ClassifyOutcome(TestNodeStateProperty state)
        => state switch
        {
            PassedTestNodeStateProperty => "passed",
            SkippedTestNodeStateProperty => "skipped",
            TimeoutTestNodeStateProperty => "timedOut",
            ErrorTestNodeStateProperty => "errored",
            FailedTestNodeStateProperty => "failed",
#pragma warning disable CS0618, MTP0001 // CancelledTestNodeStateProperty is obsolete
            // Cancellation is an interruption, not an assertion failure. The RFC
            // maps it to <error> in the generated XML; classifying it as its own
            // bucket here (rather than letting it fall through to "failed") keeps
            // that mapping local to the engine's outcome switch.
            CancelledTestNodeStateProperty => "cancelled",
#pragma warning restore CS0618, MTP0001
            _ when Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties, state.GetType()) >= 0
                => "failed",
            _ => throw ApplicationStateGuard.Unreachable(),
        };

    internal static string? Truncate(string? value, int maxLength)
    {
        if (value is null || value.Length <= maxLength)
        {
            return value;
        }

        // Don't split a surrogate pair when truncating: drop the high surrogate too.
        int cut = maxLength;
        if (cut > 0 && char.IsHighSurrogate(value[cut - 1]))
        {
            cut--;
        }

        return value.Substring(0, cut)
            + $"\n…[truncated, original length: {value.Length.ToString(CultureInfo.InvariantCulture)}]";
    }
}
