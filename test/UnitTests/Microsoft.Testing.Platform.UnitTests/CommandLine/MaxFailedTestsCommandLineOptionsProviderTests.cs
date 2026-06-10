// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.UnitTests.Helpers;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestClass]
public sealed class MaxFailedTestsCommandLineOptionsProviderTests
{
    [TestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("-100")]
    [DataRow("abc")]
    [DataRow("1.5")]
    public async Task ValidateOptionArgumentsAsync_InvalidIntegerArgument_ReturnsInvalid(string argument)
    {
        var serviceProvider = new ServiceProvider();
        var provider = new MaxFailedTestsCommandLineOptionsProvider(new TestExtension(), serviceProvider);
        CommandLineOption option = provider.GetCommandLineOptions().Single();

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [argument]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, PlatformResources.MaxFailedTestsMustBePositive, argument),
            result.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_ValidArgument_WithoutCapability_ReturnsInvalid()
    {
        var serviceProvider = new ServiceProvider();
        serviceProvider.AddService(new TestFrameworkCapabilities()); // registers ITestFrameworkCapabilities with no IGracefulStopTestExecutionCapability
        var provider = new MaxFailedTestsCommandLineOptionsProvider(new TestExtension(), serviceProvider);
        CommandLineOption option = provider.GetCommandLineOptions().Single();

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, ["1"]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(PlatformResources.AbortForMaxFailedTestsCapabilityNotAvailable, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("5")]
    [DataRow("100")]
    public async Task ValidateOptionArgumentsAsync_ValidArgument_WithCapability_ReturnsValid(string argument)
    {
        var serviceProvider = new ServiceProvider();
#pragma warning disable TPEXP // IGracefulStopTestExecutionCapability is experimental
        serviceProvider.AddService(new TestFrameworkCapabilities(new MockGracefulStopCapability()));
#pragma warning restore TPEXP
        var provider = new MaxFailedTestsCommandLineOptionsProvider(new TestExtension(), serviceProvider);
        CommandLineOption option = provider.GetCommandLineOptions().Single();

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [argument]).ConfigureAwait(false);

        Assert.IsTrue(result.IsValid);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void GetCommandLineOptions_ReturnsMaxFailedTestsOption()
    {
        var serviceProvider = new ServiceProvider();
        var provider = new MaxFailedTestsCommandLineOptionsProvider(new TestExtension(), serviceProvider);

        IReadOnlyCollection<CommandLineOption> options = provider.GetCommandLineOptions();

        Assert.HasCount(1, options);
        CommandLineOption option = options.Single();
        Assert.AreEqual(MaxFailedTestsCommandLineOptionsProvider.MaxFailedTestsOptionKey, option.Name);
        Assert.IsFalse(option.IsHidden);
        Assert.AreEqual(ArgumentArity.ExactlyOne, option.Arity);
    }

#pragma warning disable TPEXP // IGracefulStopTestExecutionCapability is experimental
    private sealed class MockGracefulStopCapability : IGracefulStopTestExecutionCapability
    {
        public Task StopTestExecutionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
#pragma warning restore TPEXP
}
