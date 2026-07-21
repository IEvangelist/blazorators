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
