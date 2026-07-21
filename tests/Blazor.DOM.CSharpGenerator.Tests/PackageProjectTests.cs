using System.Xml.Linq;
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
                $@"Blazor.DOM.Generated\{hostDirectory}\Interfaces",
                StringComparison.Ordinal));
        Assert.Contains(
            values,
            value => value.Contains(
                $@"Blazor.DOM.Generated\{hostDirectory}\Factories",
                StringComparison.Ordinal));
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
            project.Descendants("Content")
                .Select(element => element.Attribute("Include")?.Value),
            value => value?.EndsWith(
                @"Blazor.DOM\wwwroot\blazorators.dom.js",
                StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("Blazor.WakeLock", "WakeLock", "Server")]
    [InlineData("Blazor.WakeLock.WebAssembly", "WakeLock", "WebAssembly")]
    [InlineData("Blazor.Permissions", "Permissions", "Server")]
    [InlineData("Blazor.Permissions.WebAssembly", "Permissions", "WebAssembly")]
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

        var profile = Path.Combine(
            root,
            "data",
            "Blazor.DOM.Generated",
            "Profiles",
            profileName);
        Assert.True(File.Exists(Path.Combine(
            profile,
            host,
            "host-manifest.json")));
        Assert.True(File.Exists(Path.Combine(profile, "host-parity.json")));
        Assert.True(File.Exists(Path.Combine(profile, "profile-coverage.json")));
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
            "Blazor.Permissions.WebAssembly",
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
