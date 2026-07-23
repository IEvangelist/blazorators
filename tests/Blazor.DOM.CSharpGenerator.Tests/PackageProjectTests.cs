using System.Xml.Linq;
using Blazor.DOM.CSharpGenerator.Anchors;
using Blazor.DOM.CSharpGenerator.Hosts;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Profiles;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class PackageProjectTests
{
    [Theory]
    [InlineData("Blazor.DOM", "Server")]
    [InlineData("Blazor.DOM.WebAssembly", "WebAssembly")]
    public void Package_IncludesGeneratedHostAssets(
        string projectName,
        string hostDirectory)
    {
        var root = FindRepositoryRoot();
        var project = XDocument.Load(
            Path.Combine(root, "src", projectName, $"{projectName}.csproj"));
        var values = project.Descendants()
            .SelectMany(element => element.Attributes().Select(attribute => attribute.Value))
            .Concat(project.Descendants().Select(element => element.Value))
            .ToList();

        Assert.Contains(
            values,
            value => value.Contains(
                "Blazor.DOM.Generation.targets",
                StringComparison.Ordinal));
        Assert.Equal(
            hostDirectory,
            project.Descendants("DomGenerationHost").Single().Value);
        Assert.Contains(
            values,
            value => value.Contains("host-manifest.json", StringComparison.Ordinal));
        Assert.Contains(
            values,
            value => value.Contains("host-parity.json", StringComparison.Ordinal));
        Assert.Equal(
            "LICENSE",
            project.Descendants("PackageLicenseFile").Single().Value);
        Assert.Equal(
            "README.md",
            project.Descendants("PackageReadmeFile").Single().Value);
    }

    [Fact]
    public void WebAssemblyPackage_IsMutuallyExclusiveAndOwnsSharedRuntime()
    {
        var root = FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(
            root,
            "src",
            "Blazor.DOM.WebAssembly",
            "Blazor.DOM.WebAssembly.csproj"));

        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Contains(
            project.Descendants("Compile")
                .Select(element => element.Attribute("Include")?.Value),
            value => value?.Contains(
                @"Blazor.DOM\Abstractions\**\*.cs",
                StringComparison.Ordinal) == true);
        Assert.Contains(
            project.Descendants("_SharedDomStaticAssetSource")
                .Select(element => element.Value),
            value => value?.Contains(
                @"Blazor.DOM\wwwroot\blazorators.dom.js",
                StringComparison.Ordinal) == true);
        Assert.Contains(
            project.Descendants("Content"),
            element => string.Equals(
                element.Attribute("Link")?.Value,
                @"wwwroot\blazorators.dom.js",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Blazor.WakeLock", "WakeLock", "Server")]
    [InlineData("Blazor.WakeLock.WebAssembly", "WakeLock", "WebAssembly")]
    [InlineData("Blazor.Permissions", "Permissions", "Server")]
    [InlineData("Blazor.Permissions.WebAssembly", "Permissions", "WebAssembly")]
    [InlineData("Blazor.Clipboard", "Clipboard", "Server")]
    [InlineData("Blazor.Clipboard.WebAssembly", "Clipboard", "WebAssembly")]
    [InlineData("Blazor.Share", "Share", "Server")]
    [InlineData("Blazor.Share.WebAssembly", "Share", "WebAssembly")]
    [InlineData("Blazor.StorageManagement", "StorageManagement", "Server")]
    [InlineData("Blazor.StorageManagement.WebAssembly", "StorageManagement", "WebAssembly")]
    [InlineData("Blazor.Screen", "Screen", "Server")]
    [InlineData("Blazor.Screen.WebAssembly", "Screen", "WebAssembly")]
    [InlineData("Blazor.OfflineStorage", "OfflineStorage", "Server")]
    [InlineData("Blazor.OfflineStorage.WebAssembly", "OfflineStorage", "WebAssembly")]
    [InlineData("Blazor.BrowserCoordination", "BrowserCoordination", "Server")]
    [InlineData("Blazor.BrowserCoordination.WebAssembly", "BrowserCoordination", "WebAssembly")]
    [InlineData("Blazor.Performance", "Performance", "Server")]
    [InlineData("Blazor.Performance.WebAssembly", "Performance", "WebAssembly")]
    [InlineData("Blazor.Credentials", "Credentials", "Server")]
    [InlineData("Blazor.Credentials.WebAssembly", "Credentials", "WebAssembly")]
    [InlineData("Blazor.WebCrypto", "WebCrypto", "Server")]
    [InlineData("Blazor.WebCrypto.WebAssembly", "WebCrypto", "WebAssembly")]
    [InlineData("Blazor.MediaDevices", "MediaDevices", "Server")]
    [InlineData("Blazor.MediaDevices.WebAssembly", "MediaDevices", "WebAssembly")]
    [InlineData("Blazor.Notifications", "Notifications", "Server")]
    [InlineData("Blazor.Notifications.WebAssembly", "Notifications", "WebAssembly")]
    public void FocusedPackage_UsesGeneratedProfileAssets(
        string projectName,
        string profileName,
        string host)
    {
        var root = FindRepositoryRoot();
        var project = XDocument.Load(
            Path.Combine(root, "src", projectName, $"{projectName}.csproj"));
        var import = project.Descendants("Import")
            .Single(element => element.Attribute("Project")?.Value.Contains(
                "Blazor.DOM.FocusedPackage.props",
                StringComparison.Ordinal) == true);

        Assert.NotNull(import);
        Assert.Equal(
            profileName,
            project.Descendants("DomProfileName").Single().Value);
        Assert.Equal(
            host,
            project.Descendants("DomProfileHost").Single().Value);

        var focusedProps = XDocument.Load(Path.Combine(
            root,
            "src",
            "Blazor.DOM.FocusedPackage.props"));
        Assert.Contains(
            focusedProps.Descendants("Import"),
            element => element.Attribute("Project")?.Value.Contains(
                "Blazor.DOM.Generation.targets",
                StringComparison.Ordinal) == true);
        Assert.DoesNotContain(
            focusedProps.Descendants()
                .SelectMany(element => element.Attributes())
                .Select(attribute => attribute.Value),
            value => value.Contains("data\\Blazor.DOM.Generated", StringComparison.Ordinal));
    }

    [Fact]
    public void FocusedPackages_AlignRuntimeAssembliesWithStaticAssetPackagePaths()
    {
        var root = FindRepositoryRoot();
        var projects = Directory
            .EnumerateFiles(
                Path.Combine(root, "src"),
                "*.csproj",
                SearchOption.AllDirectories)
            .Select(path => (Path: path, Project: XDocument.Load(path)))
            .Where(item => item.Project.Descendants("Import").Any(
                element => element.Attribute("Project")?.Value.Contains(
                    "Blazor.DOM.FocusedPackage.props",
                    StringComparison.Ordinal) == true))
            .ToList();

        Assert.NotEmpty(projects);
        var focusedProps = XDocument.Load(Path.Combine(
            root,
            "src",
            "Blazor.DOM.FocusedPackage.props"));
        Assert.Contains(
            "System.StringComparison.Ordinal",
            focusedProps.Descendants("Target")
                .Single(element =>
                    element.Attribute("Name")?.Value
                        == "ValidateFocusedDomStaticAssetBasePath")
                .Attribute("Condition")?.Value,
            StringComparison.Ordinal);
        foreach (var (_, project) in projects)
        {
            Assert.Equal(
                project.Descendants("PackageId").Single().Value,
                project.Descendants("AssemblyName").Single().Value);
        }
    }

    [Theory]
    [InlineData("WakeLock", 24)]
    [InlineData("Permissions", 23)]
    [InlineData("Clipboard", 7)]
    [InlineData("Share", 5)]
    [InlineData("StorageManagement", 11)]
    [InlineData("Screen", 30)]
    [InlineData("OfflineStorage", 252)]
    [InlineData("BrowserCoordination", 64)]
    [InlineData("Performance", 126)]
    [InlineData("Credentials", 43)]
    [InlineData("WebCrypto", 29)]
    [InlineData("MediaDevices", 76)]
    [InlineData("Notifications", 30)]
    public void FocusedPackage_HostPairsHaveExpectedExactParity(
        string profileName,
        int operationCount)
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var output = Path.Combine(
            Path.GetTempPath(),
            Path.GetRandomFileName());
        Directory.CreateDirectory(output);
        try
        {
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
            var result = ProfilePipeline.Run(
                profile,
                IrLoader.Load(data),
                output,
                EmitterOverridesLoader.Load(data));
            var hosts = Assert.IsType<HostPackageGenerationResult>(
                result.PipelineResult.HostPackages);

            Assert.True(hosts.Parity.Exact);
            Assert.Equal(operationCount, hosts.Server.Operations.Count);
            Assert.Equal(operationCount, hosts.WebAssembly.Operations.Count);
            Assert.Equal(
                hosts.Server.Operations.Select(operation => operation.LogicalIdentity),
                hosts.WebAssembly.Operations.Select(operation => operation.LogicalIdentity));
            Assert.Empty(result.Coverage.Errors);
            Assert.True(result.Coverage.ByteIdentityVerified);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void ExistingPermissionsWebAssemblyPackage_PreservesLegacyAnalyzerSurface()
    {
        var root = FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(
            root,
            "src",
            "Blazor.Permissions.WebAssembly",
            "Blazor.Permissions.WebAssembly.csproj"));

        Assert.Contains(
            project.Descendants("ProjectReference"),
            reference => string.Equals(
                reference.Attribute("OutputItemType")?.Value,
                "Analyzer",
                StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(
            root,
            "src",
            "Blazor.DOM.Anchors",
            "Profiles",
            "Permissions",
            "IPermissionsService.cs")));
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
