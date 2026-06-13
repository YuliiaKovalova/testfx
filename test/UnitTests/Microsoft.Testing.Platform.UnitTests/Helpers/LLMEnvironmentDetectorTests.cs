// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class LLMEnvironmentDetectorTests
{
    [TestMethod]
    public void Constructor_WhenEnvironmentIsNull_ThrowsArgumentNullException()
        => Assert.ThrowsExactly<ArgumentNullException>(() => _ = new LLMEnvironmentDetector(null!));

    [TestMethod]
    public void IsLLMEnvironment_WhenNoEnvVarsSet_ReturnsFalse()
    {
        Mock<IEnvironment> environment = new();
        var detector = new LLMEnvironmentDetector(environment.Object);

        Assert.IsFalse(detector.IsLLMEnvironment());
    }

    // AnyPresentEnvironmentRule: any non-null, non-empty value triggers detection.
    [DataRow("CLAUDECODE", "1")]
    [DataRow("CLAUDE_CODE_ENTRYPOINT", "code")]
    [DataRow("CURSOR_EDITOR", "cursor")]
    [DataRow("CLINE_TASK_ID", "task-1")]
    [DataRow("ROO_CODE_TASK_ID", "roo-1")]
    [DataRow("WINDSURF_SESSION", "abc")]
    [DataRow("ZED_ENVIRONMENT", "zed")]
    [DataRow("ZED_TERM", "xterm-zed")]
    [DataRow("CODEX_CLI", "1")]
    [DataRow("CODEX_SANDBOX", "1")]
    [DataRow("GOOSE_TERMINAL", "1")]
    [DataRow("GH_COPILOT_WORKING_DIRECTORY", "/home/user")]
    [DataRow("COPILOT_CLI", "1")]
    [DataRow("AMP_HOME", "/usr/local/amp")]
    [DataRow("QWEN_CODE", "1")]
    [DataRow("OPENCODE_AI", "1")]
    [TestMethod]
    public void IsLLMEnvironment_WhenAnyPresentVariableIsSet_ReturnsTrue(string variable, string value)
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable(variable)).Returns(value);
        var detector = new LLMEnvironmentDetector(environment.Object);

        Assert.IsTrue(detector.IsLLMEnvironment());
    }

    [TestMethod]
    public void IsLLMEnvironment_WhenAnyPresentVariableIsEmpty_ReturnsFalse()
    {
        // AnyPresentEnvironmentRule requires a non-empty value; empty string is treated as not present.
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("CLAUDECODE")).Returns(string.Empty);
        var detector = new LLMEnvironmentDetector(environment.Object);

        Assert.IsFalse(detector.IsLLMEnvironment());
    }

    // BooleanEnvironmentRule recognises "true", "1", "yes", "on" (case-insensitive) as truthy.
    [DataRow("true")]
    [DataRow("True")]
    [DataRow("TRUE")]
    [DataRow("1")]
    [DataRow("yes")]
    [DataRow("Yes")]
    [DataRow("on")]
    [TestMethod]
    public void IsLLMEnvironment_WhenBooleanVariableIsTruthy_ReturnsTrue(string value)
    {
        // GEMINI_CLI uses BooleanEnvironmentRule.
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("GEMINI_CLI")).Returns(value);
        var detector = new LLMEnvironmentDetector(environment.Object);

        Assert.IsTrue(detector.IsLLMEnvironment());
    }

    // BooleanEnvironmentRule treats "false", "0", "no", "off", or arbitrary strings as falsy.
    [DataRow("false")]
    [DataRow("0")]
    [DataRow("no")]
    [DataRow("off")]
    [DataRow("random")]
    [TestMethod]
    public void IsLLMEnvironment_WhenBooleanVariableIsFalsy_ReturnsFalse(string value)
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("GEMINI_CLI")).Returns(value);
        var detector = new LLMEnvironmentDetector(environment.Object);

        Assert.IsFalse(detector.IsLLMEnvironment());
    }

    // EnvironmentVariableValueRule: OR_APP_NAME must equal the expected value (case-insensitive).
    [DataRow("Aider")]
    [DataRow("plandex")]
    [DataRow("OpenHands")]
    [TestMethod]
    public void IsLLMEnvironment_WhenValueRuleVariableMatchesExpectedValue_ReturnsTrue(string appName)
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("OR_APP_NAME")).Returns(appName);
        var detector = new LLMEnvironmentDetector(environment.Object);

        Assert.IsTrue(detector.IsLLMEnvironment());
    }

    [TestMethod]
    public void IsLLMEnvironment_WhenValueRuleVariableMatchesCaseInsensitive_ReturnsTrue()
    {
        // "AIDER" should match the rule configured with "Aider" (case-insensitive comparison).
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("OR_APP_NAME")).Returns("AIDER");
        var detector = new LLMEnvironmentDetector(environment.Object);

        Assert.IsTrue(detector.IsLLMEnvironment());
    }

    [TestMethod]
    public void IsLLMEnvironment_WhenValueRuleVariableHasUnrecognisedValue_ReturnsFalse()
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("OR_APP_NAME")).Returns("unknowntool");
        var detector = new LLMEnvironmentDetector(environment.Object);

        Assert.IsFalse(detector.IsLLMEnvironment());
    }

    // AnyMatchEnvironmentRule (Copilot): triggered by EITHER BooleanEnvironmentRule("GITHUB_COPILOT_CLI_MODE")
    // OR AnyPresentEnvironmentRule("GH_COPILOT_WORKING_DIRECTORY", "COPILOT_CLI").
    [TestMethod]
    public void IsLLMEnvironment_WhenCopilotBooleanVariableIsTrue_ReturnsTrue()
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("GITHUB_COPILOT_CLI_MODE")).Returns("true");
        var detector = new LLMEnvironmentDetector(environment.Object);

        Assert.IsTrue(detector.IsLLMEnvironment());
    }

    [TestMethod]
    public void IsLLMEnvironment_WhenCopilotBooleanVariableIsFalse_ButPresenceVarSet_ReturnsTrue()
    {
        // AnyMatchEnvironmentRule: the BooleanEnvironmentRule sub-rule is false, but the
        // AnyPresentEnvironmentRule sub-rule matches, so the overall copilot rule still triggers.
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable("GITHUB_COPILOT_CLI_MODE")).Returns("false");
        environment.Setup(e => e.GetEnvironmentVariable("GH_COPILOT_WORKING_DIRECTORY")).Returns("/workspace");
        var detector = new LLMEnvironmentDetector(environment.Object);

        Assert.IsTrue(detector.IsLLMEnvironment());
    }
}
