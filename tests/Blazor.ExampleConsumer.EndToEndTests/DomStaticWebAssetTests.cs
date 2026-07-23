using System.IO.Compression;
using System.Text.Json;

namespace Blazor.ExampleConsumer.EndToEndTests;

[Collection(DomSiteCollection.Name)]
[Trait("Category", "DOMEndToEnd")]
public sealed class DomStaticWebAssetTests(BlazoratorsSiteFixture webAssemblySite)
{
    const string AssetName = "blazorators.dom.js";
    const string AssetRequestPath = $"/_content/Blazor.DOM.WebAssembly/{AssetName}";

    [Fact]
    public async Task CanonicalStaticAssetIsValidEsModule()
    {
        var repoRoot = BlazorSiteFixture.FindRepositoryRoot();
        var assetPath = Path.Combine(
            repoRoot,
            "src",
            "Blazor.DOM",
            "wwwroot",
            AssetName);
        var result = await RunProcessAsync(
            "node",
            "--input-type=module --check",
            repoRoot,
            await File.ReadAllTextAsync(assetPath));

        Assert.True(
            result.ExitCode is 0,
            $"ES module syntax validation failed.{Environment.NewLine}{result.StandardOutput}{result.StandardError}");
    }

    [Fact]
    public async Task WebAssemblyConsumerResolvesPackageLocalPhysicalAsset()
    {
        using var client = new HttpClient();
        var asset = await client.GetByteArrayAsync(webAssemblySite.UrlFor(AssetRequestPath));

        var repoRoot = BlazorSiteFixture.FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var packageOutput = Path.Combine(
            repoRoot,
            "artifacts",
            "bin",
            "Blazor.DOM.WebAssembly",
            $"{configuration}_net10.0");
        var manifestPath = Path.Combine(
            packageOutput,
            "Blazor.DOM.WebAssembly.staticwebassets.runtime.json");

        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(manifestPath));
        var root = manifest.RootElement;
        var assetDescriptor = root
            .GetProperty("Root")
            .GetProperty("Children")
            .GetProperty(AssetName)
            .GetProperty("Asset");
        var contentRoot = root
            .GetProperty("ContentRoots")
            .EnumerateArray()
            .ElementAt(assetDescriptor.GetProperty("ContentRootIndex").GetInt32())
            .GetString()
            ?? throw new InvalidDataException("The DOM static asset content root is null.");
        var physicalAsset = Path.Combine(
            contentRoot,
            assetDescriptor.GetProperty("SubPath").GetString()
                ?? throw new InvalidDataException("The DOM static asset subpath is null."));

        Assert.True(File.Exists(physicalAsset), $"Static asset not found at {physicalAsset}.");
        Assert.Contains(
            Path.Combine("artifacts", "obj", "Blazor.DOM.WebAssembly"),
            physicalAsset,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(await File.ReadAllBytesAsync(physicalAsset), asset);
    }

    [Fact]
    public async Task WebAssemblyPackageContainsCanonicalStaticAssetAtExpectedPath()
    {
        var repoRoot = BlazorSiteFixture.FindRepositoryRoot();
        var packageProject = Path.Combine(
            repoRoot,
            "src",
            "Blazor.DOM.WebAssembly",
            "Blazor.DOM.WebAssembly.csproj");
        var packageOutput = Path.Combine(
            Path.GetTempPath(),
            $"blazor-dom-package-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(packageOutput);
            var result = await RunProcessAsync(
                "dotnet",
                $"pack \"{packageProject}\" --configuration Release --no-restore --output \"{packageOutput}\" --nologo --disable-build-servers",
                repoRoot);
            Assert.True(
                result.ExitCode is 0,
                $"dotnet pack failed.{Environment.NewLine}{result.StandardOutput}{result.StandardError}");

            var packagePath = Assert.Single(
                Directory.GetFiles(packageOutput, "Blazor.DOM.WebAssembly*.nupkg"));
            using var package = ZipFile.OpenRead(packagePath);
            var packedAsset = Assert.Single(
                package.Entries,
                entry => entry.FullName == $"staticwebassets/{AssetName}");
            await using var packedStream = packedAsset.Open();
            using var packedBytes = new MemoryStream();
            await packedStream.CopyToAsync(packedBytes);

            var canonicalAsset = await File.ReadAllBytesAsync(
                Path.Combine(
                    repoRoot,
                    "src",
                    "Blazor.DOM",
                    "wwwroot",
                    AssetName));
            Assert.Equal(canonicalAsset, packedBytes.ToArray());
        }
        finally
        {
            Directory.Delete(packageOutput, recursive: true);
        }
    }

    static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        string? standardInput = null)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException($"Unable to start {fileName}.");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput);
            process.StandardInput.Close();
        }

        await process.WaitForExitAsync();

        return new(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
