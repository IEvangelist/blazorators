using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Hosts;
using Blazor.DOM.CSharpGenerator.Profiles;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class FocusedPackageGenerationTests
{
    [Fact]
    public void BinaryConstrainedGenericProfile_EmitsSupportedHostPair()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var output = CreateTempDir();
        try
        {
            var profile = new ProfileDefinition(
                "CryptoBinary",
                "Binary-constrained generic fixture.",
                ["Crypto"],
                true,
                false,
                ["web-crypto"],
                "Blazor.DOM",
                "Profiles/CryptoBinary",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["Crypto"] = ["getRandomValues"],
                },
                true,
                EntryPoints:
                [
                    new HostEntryPoint("Crypto", "Crypto", "crypto"),
                ]);

            var result = ProfilePipeline.Run(
                profile,
                IrLoader.Load(data),
                output,
                EmitterOverridesLoader.Load(data));

            Assert.Empty(result.PipelineResult.Errors);
            var hosts = Assert.IsType<HostPackageGenerationResult>(
                result.PipelineResult.HostPackages);
            Assert.True(hosts.Parity.Exact);

            var generated = Path.Combine(
                output,
                "Profiles",
                "CryptoBinary",
                "Server",
                "Interfaces",
                "ICrypto.g.cs");
            var source = File.ReadAllText(generated);
            Assert.Contains(
                "where T : global::System.Collections.IList",
                source,
                StringComparison.Ordinal);
            Assert.Contains(
                "DomTransportKind.Binary",
                source,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "DomTransportKind.Unsupported",
                source,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(output, true);
        }
    }

    [Fact]
    public void GenericResultProfile_UsesReviewedTransportOverride()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var output = CreateTempDir();
        try
        {
            var profile = new ProfileDefinition(
                "GenericResult",
                "Reviewed generic result transport fixture.",
                ["IDBRequest"],
                false,
                false,
                [],
                "Blazor.DOM",
                "Profiles/GenericResult",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["IDBRequest"] = ["result"],
                },
                true,
                EntryPoints:
                [
                    new HostEntryPoint(
                        "IDBRequest",
                        "IDBRequest",
                        "indexedDB.fixture"),
                ],
                TransportOverrides:
                [
                    new ProfileTransportOverride(
                        "IDBRequest",
                        "result",
                        "runtime-inferred",
                        "The closed CLR result determines proxy or value transport."),
                ]);

            var result = ProfilePipeline.Run(
                profile,
                IrLoader.Load(data),
                output,
                EmitterOverridesLoader.Load(data));

            Assert.Empty(result.PipelineResult.Errors);
            var generated = Path.Combine(
                output,
                "Profiles",
                "GenericResult",
                "Server",
                "Interfaces",
                "IIDBRequest.g.cs");
            var source = File.ReadAllText(generated);
            Assert.Contains(
                "DomTransportKind.Inferred",
                source,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "DomTransportKind.Unsupported",
                source,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(output, true);
        }
    }

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
    public void PackageProfile_AllowsSupportedAmbientTypeScriptReferences()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var output = CreateTempDir();
        try
        {
            var profile = new ProfileDefinition(
                "AmbientReferences",
                "Supported TypeScript ambient references.",
                ["IDBObjectStore"],
                false,
                false,
                [],
                "Blazor.DOM",
                "Profiles/AmbientReferences",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["IDBObjectStore"] = ["add", "getAllKeys"],
                },
                true,
                EntryPoints:
                [
                    new HostEntryPoint(
                        "IDBObjectStore",
                        "IDBObjectStore",
                        "indexedDB.fixture"),
                ]);

            var exception = Record.Exception(() => ProfilePipeline.Run(
                profile,
                IrLoader.Load(data),
                output,
                EmitterOverridesLoader.Load(data)));

            Assert.False(
                exception?.Message.Contains(
                    "closure leaks unresolved reference",
                    StringComparison.Ordinal) == true,
                exception?.ToString());
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
