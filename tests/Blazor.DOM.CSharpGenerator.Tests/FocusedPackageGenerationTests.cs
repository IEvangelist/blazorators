using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Hosts;
using Blazor.DOM.CSharpGenerator.Profiles;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class FocusedPackageGenerationTests
{
    [Fact]
    public void PackageProfile_EmitsDeterministicHostPairsAndCapabilityMetadata()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var output = CreateTempDir();
        try
        {
            var profile = new ProfileDefinition(
                "WakeLock",
                "Screen wake lock.",
                ["WakeLock", "WakeLockSentinel"],
                true,
                false,
                ["screen-wake-lock"],
                "Blazor.DOM",
                "Profiles/WakeLock",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["WakeLock"] = ["request"],
                    ["WakeLockSentinel"] =
                    [
                        "released",
                        "type",
                        "release",
                        "addEventListener",
                        "removeEventListener",
                    ],
                    ["EventListener"] = ["*"],
                    ["EventListenerObject"] = ["*"],
                    ["WakeLockSentinelEventMap"] = ["*"],
                },
                true,
                EntryPoints:
                [
                    new HostEntryPoint(
                        "WakeLock",
                        "WakeLock",
                        "navigator.wakeLock"),
                ]);

            var result = ProfilePipeline.Run(
                profile,
                IrLoader.Load(data),
                output,
                EmitterOverridesLoader.Load(data));

            Assert.True(result.Coverage.ByteIdentityVerified);
            Assert.NotNull(result.PipelineResult.HostPackages);
            Assert.True(result.PipelineResult.HostPackages.Parity.Exact);
            Assert.Equal(
                result.PipelineResult.HostPackages.Server.Operations
                    .Select(operation => operation.LogicalIdentity),
                result.PipelineResult.HostPackages.WebAssembly.Operations
                    .Select(operation => operation.LogicalIdentity));

            var generated = Path.Combine(output, "Profiles", "WakeLock");
            Assert.True(File.Exists(Path.Combine(
                generated,
                "Server",
                "GeneratedDomHost.g.cs")));
            Assert.True(File.Exists(Path.Combine(
                generated,
                "WebAssembly",
                "GeneratedDomHost.g.cs")));
            Assert.True(File.Exists(Path.Combine(generated, "host-parity.json")));

            var serverSource = File.ReadAllText(Path.Combine(
                generated,
                "Server",
                "GeneratedDomHost.g.cs"));
            Assert.Contains(
                "IWakeLockCapability",
                serverSource,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"navigator.wakeLock\"",
                serverSource,
                StringComparison.Ordinal);
            Assert.Contains(
                "RequiresSecureContext = true",
                serverSource,
                StringComparison.Ordinal);

            var wasmSource = File.ReadAllText(Path.Combine(
                generated,
                "WebAssembly",
                "GeneratedDomHost.g.cs"));
            Assert.Contains(
                "global::Blazor.DOM.IWakeLock GetWakeLock()",
                wasmSource,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(output, true);
        }
    }

    [Fact]
    public void PackageProfile_MissingRootFailsWithoutCanonicalOutput()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var output = CreateTempDir();
        try
        {
            var profile = new ProfileDefinition(
                "Missing",
                "Missing root.",
                ["NotInTheSemanticModel"],
                false,
                false,
                [],
                "Blazor.DOM",
                "Profiles/Missing",
                EntryPoints:
                [
                    new HostEntryPoint(
                        "Missing",
                        "NotInTheSemanticModel",
                        "navigator.missing"),
                ]);

            Assert.Throws<InvalidDataException>(() => ProfilePipeline.Run(
                profile,
                IrLoader.Load(data),
                output,
                EmitterOverridesLoader.Load(data)));
            Assert.False(Directory.Exists(Path.Combine(
                output,
                "Profiles",
                "Missing")));
        }

        finally
        {
            Directory.Delete(output, true);
        }
    }

    [Fact]
    public void PackageProfile_UnsupportedTransportFailsWithoutCanonicalOutput()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var output = CreateTempDir();
        try
        {
            var profile = new ProfileDefinition(
                "Unsupported",
                "Unfiltered wake lock.",
                ["WakeLock", "WakeLockSentinel"],
                true,
                false,
                ["screen-wake-lock"],
                "Blazor.DOM",
                "Profiles/Unsupported",
                EntryPoints:
                [
                    new HostEntryPoint(
                        "WakeLock",
                        "WakeLock",
                        "navigator.wakeLock"),
                ]);

            Assert.Throws<InvalidDataException>(() => ProfilePipeline.Run(
                profile,
                IrLoader.Load(data),
                output,
                EmitterOverridesLoader.Load(data)));
            Assert.False(Directory.Exists(Path.Combine(
                output,
                "Profiles",
                "Unsupported")));
        }
        finally
        {
            Directory.Delete(output, true);
        }
    }

    [Fact]
    public void GlobalConstructorEntryPoint_UsesFactoryContract()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var output = CreateTempDir();
        try
        {
            var profile = new ProfileDefinition(
                "Notifications",
                "Notifications.",
                ["Notification"],
                true,
                true,
                ["notifications"],
                "Blazor.DOM",
                "Profiles/Notifications",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["Notification"] =
                    [
                        "title",
                        "close",
                        "addEventListener",
                        "removeEventListener",
                    ],
                    ["EventListener"] = ["*"],
                    ["EventListenerObject"] = ["*"],
                    ["NotificationEventMap"] = ["*"],
                    ["NotificationPermissionCallback"] = ["*"],
                },
                true,
                EntryPoints:
                [
                    new HostEntryPoint(
                        "Notification",
                        "Notification",
                        "Notification"),
                ],
                Permissions: ["notifications"]);

            var result = ProfilePipeline.Run(
                profile,
                IrLoader.Load(data),
                output,
                EmitterOverridesLoader.Load(data));

            Assert.True(
                result.PipelineResult.Errors.Count == 0,
                string.Join(
                    Environment.NewLine,
                    result.PipelineResult.Errors.Select(error => error.Message)));
            Assert.True(
                result.PipelineResult.Validation.IsValid,
                string.Join(
                    Environment.NewLine,
                    result.PipelineResult.Validation.Diagnostics));
            var generated = Path.Combine(output, "Profiles", "Notifications");
            var serverSource = File.ReadAllText(Path.Combine(
                generated,
                "Server",
                "GeneratedDomHost.g.cs"));
            var wasmSource = File.ReadAllText(Path.Combine(
                generated,
                "WebAssembly",
                "GeneratedDomHost.g.cs"));
            var serverFactory = File.ReadAllText(Path.Combine(
                generated,
                "Server",
                "Factories",
                "INotificationFactory.g.cs"));
            var wasmFactory = File.ReadAllText(Path.Combine(
                generated,
                "WebAssembly",
                "Factories",
                "INotificationFactory.g.cs"));

            Assert.True(result.PipelineResult.HostPackages!.Parity.Exact);
            Assert.Equal(["notifications"], result.Coverage.Permissions);
            Assert.Contains(
                "ValueTask<global::Blazor.DOM.INotificationFactory> GetNotificationAsync",
                serverSource,
                StringComparison.Ordinal);
            Assert.Contains(
                "global::Blazor.DOM.INotificationFactory GetNotification()",
                wasmSource,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "ValueTask<global::Blazor.DOM.INotification> GetNotificationAsync",
                serverSource,
                StringComparison.Ordinal);
            Assert.Contains(
                "IReadOnlyList<string> Permissions",
                serverSource,
                StringComparison.Ordinal);
            Assert.Contains(
                "[\"notifications\"]",
                serverSource,
                StringComparison.Ordinal);
            Assert.Contains(
                "ValueTask<NotificationPermission> RequestPermissionAsync(",
                serverFactory,
                StringComparison.Ordinal);
            Assert.Contains(
                "DomDispatch.InvokeAsync<NotificationPermission>",
                serverFactory,
                StringComparison.Ordinal);
            Assert.Contains(
                "ValueTask<NotificationPermission> RequestPermissionAsync(",
                wasmFactory,
                StringComparison.Ordinal);
            Assert.Contains(
                "DomDispatch.InvokeAsync<NotificationPermission>",
                wasmFactory,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "ValueTask<ValueTask<NotificationPermission>>",
                serverFactory,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "WasmDomDispatch.Invoke<ValueTask<NotificationPermission>>",
                wasmFactory,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(output, true);
        }
    }

    private static string CreateTempDir()
    {
        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "blazorators.sln")))
                return directory;
            directory = Path.GetDirectoryName(directory);
        }
        throw new DirectoryNotFoundException("Could not locate blazorators.sln.");
    }
}
