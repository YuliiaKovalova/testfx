// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class LLMEnvironmentDetectorTests
{
    [TestMethod]
    public void IsLLMEnvironment_NoVariablesSet_ReturnsFalse()
    {
        Mock<IEnvironment> env = new();
        env.Setup(e => e.GetEnvironmentVariable(It.IsAny<string>())).Returns((string?)null);

        Assert.IsFalse(new LLMEnvironmentDetector(env.Object).IsLLMEnvironment());
    }

    [TestMethod]
    [DataRow("CLAUDECODE")]
    [DataRow("CLAUDE_CODE_ENTRYPOINT")]
    public void IsLLMEnvironment_ClaudeVariable_ReturnsTrue(string variable)
        => AssertDetectedWhenPresent(variable);

    [TestMethod]
    [DataRow("CURSOR_EDITOR")]
    [DataRow("CURSOR_AI")]
    public void IsLLMEnvironment_CursorVariable_ReturnsTrue(string variable)
        => AssertDetectedWhenPresent(variable);

    [TestMethod]
    [DataRow("CODEX_CLI")]
    [DataRow("CODEX_SANDBOX")]
    public void IsLLMEnvironment_CodexVariable_ReturnsTrue(string variable)
        => AssertDetectedWhenPresent(variable);

    [TestMethod]
    [DataRow("GH_COPILOT_WORKING_DIRECTORY")]
    [DataRow("COPILOT_CLI")]
    public void IsLLMEnvironment_CopilotPresentVariable_ReturnsTrue(string variable)
        => AssertDetectedWhenPresent(variable);

    [TestMethod]
    [DataRow("AMP_HOME")]
    [DataRow("QWEN_CODE")]
    [DataRow("OPENCODE_AI")]
    [DataRow("ZED_ENVIRONMENT")]
    [DataRow("ZED_TERM")]
    [DataRow("GOOSE_TERMINAL")]
    [DataRow("CLINE_TASK_ID")]
    [DataRow("ROO_CODE_TASK_ID")]
    [DataRow("WINDSURF_SESSION")]
    public void IsLLMEnvironment_OtherPresentVariable_ReturnsTrue(string variable)
        => AssertDetectedWhenPresent(variable);

    [TestMethod]
    public void IsLLMEnvironment_PresentVariableWithEmptyString_ReturnsFalse()
    {
        // AnyPresentEnvironmentRule uses IsNullOrEmpty — an empty value is treated as absent.
        Mock<IEnvironment> env = new();
        env.Setup(e => e.GetEnvironmentVariable("CLAUDECODE")).Returns(string.Empty);

        Assert.IsFalse(new LLMEnvironmentDetector(env.Object).IsLLMEnvironment());
    }

    [TestMethod]
    [DataRow("GEMINI_CLI", "true")]
    [DataRow("GEMINI_CLI", "1")]
    [DataRow("GEMINI_CLI", "yes")]
    [DataRow("GEMINI_CLI", "on")]
    [DataRow("GEMINI_CLI", "TRUE")] // case-insensitive boolean parsing
    [DataRow("DROID_CLI", "1")]
    [DataRow("KIMI_CLI", "true")]
    [DataRow("AGENT_CLI", "yes")]
    [DataRow("GITHUB_COPILOT_CLI_MODE", "true")]
    [DataRow("GITHUB_COPILOT_CLI_MODE", "1")]
    public void IsLLMEnvironment_BooleanVariableSetToTruthy_ReturnsTrue(string variable, string value)
    {
        Mock<IEnvironment> env = new();
        env.Setup(e => e.GetEnvironmentVariable(variable)).Returns(value);

        Assert.IsTrue(new LLMEnvironmentDetector(env.Object).IsLLMEnvironment());
    }

    [TestMethod]
    [DataRow("GEMINI_CLI", "false")]
    [DataRow("GEMINI_CLI", "0")]
    [DataRow("GEMINI_CLI", "no")]
    [DataRow("GEMINI_CLI", "off")]
    [DataRow("AGENT_CLI", "0")]
    [DataRow("GITHUB_COPILOT_CLI_MODE", "false")]
    public void IsLLMEnvironment_BooleanVariableSetToFalsy_ReturnsFalse(string variable, string value)
    {
        Mock<IEnvironment> env = new();
        env.Setup(e => e.GetEnvironmentVariable(variable)).Returns(value);

        Assert.IsFalse(new LLMEnvironmentDetector(env.Object).IsLLMEnvironment());
    }

    [TestMethod]
    [DataRow("Aider")]
    [DataRow("AIDER")] // case-insensitive match
    [DataRow("aider")]
    public void IsLLMEnvironment_ORAppNameAider_ReturnsTrue(string value)
        => AssertDetectedWithORAppName(value);

    [TestMethod]
    [DataRow("plandex")]
    [DataRow("PLANDEX")] // case-insensitive match
    [DataRow("Plandex")]
    public void IsLLMEnvironment_ORAppNamePlandex_ReturnsTrue(string value)
        => AssertDetectedWithORAppName(value);

    [TestMethod]
    [DataRow("OpenHands")]
    [DataRow("OPENHANDS")] // case-insensitive match
    [DataRow("openhands")]
    public void IsLLMEnvironment_ORAppNameOpenHands_ReturnsTrue(string value)
        => AssertDetectedWithORAppName(value);

    [TestMethod]
    public void IsLLMEnvironment_ORAppNameUnknownValue_ReturnsFalse()
    {
        Mock<IEnvironment> env = new();
        env.Setup(e => e.GetEnvironmentVariable("OR_APP_NAME")).Returns("SomethingElse");

        Assert.IsFalse(new LLMEnvironmentDetector(env.Object).IsLLMEnvironment());
    }

    private static void AssertDetectedWhenPresent(string variable)
    {
        Mock<IEnvironment> env = new();
        env.Setup(e => e.GetEnvironmentVariable(variable)).Returns("set");

        Assert.IsTrue(new LLMEnvironmentDetector(env.Object).IsLLMEnvironment());
    }

    private static void AssertDetectedWithORAppName(string value)
    {
        Mock<IEnvironment> env = new();
        env.Setup(e => e.GetEnvironmentVariable("OR_APP_NAME")).Returns(value);

        Assert.IsTrue(new LLMEnvironmentDetector(env.Object).IsLLMEnvironment());
    }
}
