using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Hosts;
using Blazor.DOM.CSharpGenerator.Profiles;
using Blazor.DOM.CSharpGenerator.Anchors;
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

    [Fact]
    public void MediaDevicesProfile_EmitsExactProxyTransportAndLifecycleSurface()
    {
        var output = CreateTempDir();
        try
        {
            var result = GenerateRepositoryProfile("MediaDevices", output);
            var hosts = Assert.IsType<HostPackageGenerationResult>(
                result.PipelineResult.HostPackages);
            var generated = Path.Combine(output, "Profiles", "MediaDevices");
            var serverHost = ReadGenerated(
                generated, "Server", "GeneratedDomHost.g.cs");
            var wasmHost = ReadGenerated(
                generated, "WebAssembly", "GeneratedDomHost.g.cs");
            var serverMediaDevices = ReadGenerated(
                generated, "Server", "Interfaces", "IMediaDevices.g.cs");
            var wasmMediaDevices = ReadGenerated(
                generated, "WebAssembly", "Interfaces", "IMediaDevices.g.cs");
            var serverTrack = ReadGenerated(
                generated, "Server", "Interfaces", "IMediaStreamTrack.g.cs");
            var allHostSources = string.Join(
                Environment.NewLine,
                Directory.EnumerateFiles(generated, "*.cs", SearchOption.AllDirectories)
                    .Where(path => path.Contains(
                        $"{Path.DirectorySeparatorChar}Server{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal)
                        || path.Contains(
                            $"{Path.DirectorySeparatorChar}WebAssembly{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal))
                    .Select(File.ReadAllText));

            Assert.True(hosts.Parity.Exact);
            Assert.Equal(76, hosts.Server.Operations.Count);
            Assert.Equal(76, hosts.WebAssembly.Operations.Count);
            Assert.Equal(37, result.IncludedSymbolCount);
            Assert.Equal(38, result.ClosureSize);
            Assert.Equal(1, result.ExternalReferenceCount);
            Assert.Equal(["Promise"], result.Coverage.ExternalReferences);
            Assert.Equal(18, hosts.Server.GeneratedFiles.Count);
            Assert.Equal(18, hosts.WebAssembly.GeneratedFiles.Count);
            Assert.Contains(
                "ValueTask<global::Blazor.DOM.IMediaDevices> GetMediaDevicesAsync",
                serverHost,
                StringComparison.Ordinal);
            Assert.Contains(
                "global::Blazor.DOM.IMediaDevices GetMediaDevices()",
                wasmHost,
                StringComparison.Ordinal);
            Assert.Contains(
                "browser.GetGlobalAsync<global::Blazor.DOM.IMediaDevices>(\"navigator.mediaDevices\"",
                serverHost,
                StringComparison.Ordinal);
            Assert.Contains(
                "IBrowserArray<IMediaDeviceInfo>> EnumerateDevicesAsync",
                serverMediaDevices,
                StringComparison.Ordinal);
            Assert.Contains(
                "DomTransportDescriptor.JsReference(\"MediaDeviceInfo[]\"",
                serverMediaDevices,
                StringComparison.Ordinal);
            Assert.Contains(
                "ValueTask<IMediaStream> GetUserMediaAsync",
                serverMediaDevices,
                StringComparison.Ordinal);
            Assert.Contains(
                "ValueTask<IMediaStream> GetDisplayMediaAsync",
                serverMediaDevices,
                StringComparison.Ordinal);
            Assert.Contains(
                "GetCapabilitiesAsync",
                serverTrack,
                StringComparison.Ordinal);
            Assert.Contains(
                "GetConstraintsAsync",
                serverTrack,
                StringComparison.Ordinal);
            Assert.Contains(
                "GetSettingsAsync",
                serverTrack,
                StringComparison.Ordinal);
            Assert.Contains(
                "RemoveEventListenerAsync",
                serverMediaDevices,
                StringComparison.Ordinal);
            Assert.Contains(
                "RemoveEventListener(",
                wasmMediaDevices,
                StringComparison.Ordinal);
            Assert.Contains(
                ": global::Microsoft.JSInterop.DomProxyBase",
                serverMediaDevices,
                StringComparison.Ordinal);
            Assert.Contains(
                ": global::Microsoft.JSInterop.WasmDomProxyBase",
                wasmMediaDevices,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "selectAudioOutput",
                allHostSources,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "DomTransportKind.Unsupported",
                allHostSources,
                StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(
                generated, "Server", "host-manifest.json")));
            Assert.True(File.Exists(Path.Combine(
                generated, "WebAssembly", "host-manifest.json")));
            Assert.True(File.Exists(Path.Combine(generated, "host-parity.json")));
            Assert.True(File.Exists(Path.Combine(generated, "profile-coverage.json")));
        }
        finally
        {
            Directory.Delete(output, true);
        }
    }

    [Fact]
    public void NotificationsProfile_EmitsConstructorStaticEventsAndLifecycleSurface()
    {
        var output = CreateTempDir();
        try
        {
            var result = GenerateRepositoryProfile("Notifications", output);
            var hosts = Assert.IsType<HostPackageGenerationResult>(
                result.PipelineResult.HostPackages);
            var generated = Path.Combine(output, "Profiles", "Notifications");
            var serverHost = ReadGenerated(
                generated, "Server", "GeneratedDomHost.g.cs");
            var wasmHost = ReadGenerated(
                generated, "WebAssembly", "GeneratedDomHost.g.cs");
            var serverFactory = ReadGenerated(
                generated, "Server", "Factories", "INotificationFactory.g.cs");
            var wasmFactory = ReadGenerated(
                generated, "WebAssembly", "Factories", "INotificationFactory.g.cs");
            var serverNotification = ReadGenerated(
                generated, "Server", "Interfaces", "INotification.g.cs");
            var wasmNotification = ReadGenerated(
                generated, "WebAssembly", "Interfaces", "INotification.g.cs");
            var allHostSources = string.Join(
                Environment.NewLine,
                Directory.EnumerateFiles(generated, "*.cs", SearchOption.AllDirectories)
                    .Where(path => path.Contains(
                        $"{Path.DirectorySeparatorChar}Server{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal)
                        || path.Contains(
                            $"{Path.DirectorySeparatorChar}WebAssembly{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal))
                    .Select(File.ReadAllText));

            Assert.True(hosts.Parity.Exact);
            Assert.Equal(30, hosts.Server.Operations.Count);
            Assert.Equal(30, hosts.WebAssembly.Operations.Count);
            Assert.Equal(14, result.IncludedSymbolCount);
            Assert.Equal(15, result.ClosureSize);
            Assert.Equal(1, result.ExternalReferenceCount);
            Assert.Equal(["Promise"], result.Coverage.ExternalReferences);
            Assert.Equal(8, hosts.Server.GeneratedFiles.Count);
            Assert.Equal(8, hosts.WebAssembly.GeneratedFiles.Count);
            Assert.Contains(
                "ValueTask<global::Blazor.DOM.INotificationFactory> GetNotificationAsync",
                serverHost,
                StringComparison.Ordinal);
            Assert.Contains(
                "global::Blazor.DOM.INotificationFactory GetNotification()",
                wasmHost,
                StringComparison.Ordinal);
            Assert.Contains(
                "browser.GetGlobalAsync<global::Blazor.DOM.INotificationFactory>(\"Notification\"",
                serverHost,
                StringComparison.Ordinal);
            Assert.Contains(
                "ValueTask<INotification> CreateAsync(string title, NotificationOptions? options",
                serverFactory,
                StringComparison.Ordinal);
            Assert.Contains(
                "ConstructAsync<INotification>",
                serverFactory,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"Notification\"",
                serverFactory,
                StringComparison.Ordinal);
            Assert.Contains(
                "GetPermissionAsync",
                serverFactory,
                StringComparison.Ordinal);
            Assert.Contains(
                "ValueTask<NotificationPermission> RequestPermissionAsync",
                serverFactory,
                StringComparison.Ordinal);
            Assert.Contains(
                "ValueTask<NotificationPermission> RequestPermissionAsync",
                wasmFactory,
                StringComparison.Ordinal);
            Assert.Contains(
                "RemoveEventListenerAsync",
                serverNotification,
                StringComparison.Ordinal);
            Assert.Contains(
                "RemoveEventListener(",
                wasmNotification,
                StringComparison.Ordinal);
            Assert.Contains(
                ": global::Microsoft.JSInterop.DomProxyBase",
                serverNotification,
                StringComparison.Ordinal);
            Assert.Contains(
                ": global::Microsoft.JSInterop.WasmDomProxyBase",
                wasmNotification,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "maxActions",
                allHostSources,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "NotificationAction",
                allHostSources,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "DomTransportKind.Unsupported",
                allHostSources,
                StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(
                generated, "Server", "host-manifest.json")));
            Assert.True(File.Exists(Path.Combine(
                generated, "WebAssembly", "host-manifest.json")));
            Assert.True(File.Exists(Path.Combine(generated, "host-parity.json")));
            Assert.True(File.Exists(Path.Combine(generated, "profile-coverage.json")));
        }
        finally
        {
            Directory.Delete(output, true);
        }
    }

    private static ProfileGenerationResult GenerateRepositoryProfile(
            string profileName,
            string output)
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var anchors = InteropAnchorLoader.Load(Path.Combine(
            root,
            "src",
            "Blazor.DOM.Anchors"));
        var profile = InteropAnchorLoader.Apply(
            ProfileLoader.Load(Path.Combine(
                root,
                "data",
                "Blazor.DOM.Profiles",
                $"{profileName}.profile.json")),
            anchors);

        return ProfilePipeline.Run(
            profile,
            IrLoader.Load(data),
            output,
            EmitterOverridesLoader.Load(data));
    }

    private static string ReadGenerated(string root, params string[] path)
        => File.ReadAllText(path.Aggregate(root, Path.Combine));

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
