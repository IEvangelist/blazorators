using System.Diagnostics;
using System.IO.Compression;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class BuildGenerationTests
{
    [Fact]
    [Trait("Category", "BuildGraph")]
    public async Task ProductionBuildGraph_IsIncrementalRaceSafeCleanAndPackable()
    {
        var root = FindRepositoryRoot();
        var testRoot = Path.Combine(
            root,
            "artifacts",
            "obj",
            "Blazor.DOM.Generation.Tests",
            Guid.NewGuid().ToString("N"));
        var generated = Path.Combine(testRoot, "dom");
        var sentinel = Path.Combine(testRoot, "invalidation.sentinel");
        var packages = Path.Combine(testRoot, "packages");
        Directory.CreateDirectory(testRoot);
        await File.WriteAllTextAsync(sentinel, "first");

        try
        {
            var build = await RunDotNetAsync(
                root,
                "build",
                Path.Combine("src", "Blazor.DOM", "Blazor.DOM.csproj"),
                "--configuration",
                "Release",
                "--warnaserror",
                $"-p:DomGeneratedOutputRoot={generated}",
                $"-p:DomGenerationSentinel={sentinel}");
            AssertSucceeded(build);
            Assert.Equal(
                1,
                CountOccurrences(
                    build.Output,
                    "Blazor.DOM.CSharpGenerator v1.0.0"));
            Assert.NotEmpty(Directory.EnumerateFiles(
                generated,
                "*.g.cs",
                SearchOption.AllDirectories));
            Assert.True(File.Exists(Path.Combine(
                generated,
                "emitter-manifest.json")));
            Assert.True(File.Exists(Path.Combine(
                generated,
                "Profiles",
                "WakeLock",
                "profile-coverage.json")));

            var stamp = Path.Combine(generated, "generation.stamp");
            var initialStamp = File.GetLastWriteTimeUtc(stamp);
            var unchanged = await RunDotNetAsync(
                root,
                "build",
                Path.Combine("src", "Blazor.DOM", "Blazor.DOM.csproj"),
                "--configuration",
                "Release",
                "--framework",
                "net10.0",
                "--no-restore",
                $"-p:DomGeneratedOutputRoot={generated}",
                $"-p:DomGenerationSentinel={sentinel}");
            AssertSucceeded(unchanged);
            Assert.Equal(initialStamp, File.GetLastWriteTimeUtc(stamp));
            Assert.DoesNotContain(
                "Blazor.DOM.CSharpGenerator v1.0.0",
                unchanged.Output,
                StringComparison.Ordinal);

            await Task.Delay(TimeSpan.FromSeconds(1.1));
            await File.WriteAllTextAsync(sentinel, "changed");
            var invalidated = await RunDotNetAsync(
                root,
                "build",
                Path.Combine("src", "Blazor.DOM", "Blazor.DOM.csproj"),
                "--configuration",
                "Release",
                "--framework",
                "net10.0",
                "--no-restore",
                $"-p:DomGeneratedOutputRoot={generated}",
                $"-p:DomGenerationSentinel={sentinel}");
            AssertSucceeded(invalidated);
            Assert.True(File.GetLastWriteTimeUtc(stamp) > initialStamp);
            Assert.Contains(
                "Blazor.DOM.CSharpGenerator v1.0.0",
                invalidated.Output,
                StringComparison.Ordinal);

            var clean = await RunDotNetAsync(
                root,
                "clean",
                Path.Combine("src", "Blazor.DOM", "Blazor.DOM.csproj"),
                "--configuration",
                "Release",
                $"-p:DomGeneratedOutputRoot={generated}",
                $"-p:DomGenerationSentinel={sentinel}");
            AssertSucceeded(clean);
            Assert.False(Directory.Exists(generated));

            var rebuild = await RunDotNetAsync(
                root,
                "build",
                Path.Combine("src", "Blazor.DOM", "Blazor.DOM.csproj"),
                "--configuration",
                "Release",
                "--framework",
                "net10.0",
                "--no-restore",
                $"-p:DomGeneratedOutputRoot={generated}",
                $"-p:DomGenerationSentinel={sentinel}");
            AssertSucceeded(rebuild);
            Assert.True(File.Exists(stamp));

            var exhaustivePack = await RunDotNetAsync(
                root,
                "pack",
                Path.Combine(
                    "src",
                    "Blazor.DOM.WebAssembly",
                    "Blazor.DOM.WebAssembly.csproj"),
                "--configuration",
                "Release",
                "--no-restore",
                $"-p:DomGeneratedOutputRoot={generated}",
                $"-p:DomGenerationSentinel={sentinel}",
                $"-p:PackageOutputPath={packages}");
            AssertSucceeded(exhaustivePack);
            AssertPackage(
                packages,
                "Blazor.DOM.WebAssembly",
                "Blazor.DOM.WebAssembly.dll",
                "Blazor.DOM.WebAssembly.host-manifest.json",
                "Blazor.DOM.WebAssembly.host-parity.json",
                requireStaticAsset: true);

            var focusedPack = await RunDotNetAsync(
                root,
                "pack",
                Path.Combine("src", "Blazor.WakeLock", "Blazor.WakeLock.csproj"),
                "--configuration",
                "Release",
                "--no-restore",
                $"-p:DomGeneratedOutputRoot={generated}",
                $"-p:DomGenerationSentinel={sentinel}",
                $"-p:PackageOutputPath={packages}");
            AssertSucceeded(focusedPack);
            AssertPackage(
                packages,
                "Blazor.WakeLock",
                "Blazor.WakeLock.dll",
                "Blazor.WakeLock.host-manifest.json",
                "Blazor.WakeLock.host-parity.json",
                "Blazor.WakeLock.profile-coverage.json",
                requireStaticAsset: true);

            var tracked = await RunProcessAsync(
                root,
                "git",
                ["ls-files", "data/Blazor.DOM.Generated/**/*.g.cs"]);
            AssertSucceeded(tracked);
            Assert.True(string.IsNullOrWhiteSpace(tracked.Output));
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    private static void AssertPackage(
        string packageDirectory,
        string packagePrefix,
        string assembly,
        string manifest,
        string parity,
        string? coverage = null,
        bool requireStaticAsset = false)
    {
        var package = Directory
            .EnumerateFiles(packageDirectory, $"{packagePrefix}.*.nupkg")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
        using var archive = ZipFile.OpenRead(package);
        var entries = archive.Entries
            .Select(entry => entry.FullName)
            .ToList();

        Assert.Contains(entries, path => path.EndsWith(
            $"/{assembly}",
            StringComparison.Ordinal));
        Assert.Contains(entries, path => path.EndsWith(
            $"/{manifest}",
            StringComparison.Ordinal));
        Assert.Contains(entries, path => path.EndsWith(
            $"/{parity}",
            StringComparison.Ordinal));
        if (coverage is not null)
        {
            Assert.Contains(entries, path => path.EndsWith(
                $"/{coverage}",
                StringComparison.Ordinal));
        }
        if (requireStaticAsset)
        {
            Assert.Contains(entries, path => path.EndsWith(
                "/blazorators.dom.js",
                StringComparison.Ordinal));
        }
    }

    private static Task<ProcessResult> RunDotNetAsync(
        string workingDirectory,
        params string[] arguments)
        => RunProcessAsync(workingDirectory, "dotnet", arguments);

    private static async Task<ProcessResult> RunProcessAsync(
        string workingDirectory,
        string fileName,
        IReadOnlyList<string> arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static void AssertSucceeded(ProcessResult result)
        => Assert.True(
            result.ExitCode == 0,
            $"Process exited with {result.ExitCode}.{Environment.NewLine}" +
            $"{result.Output}{Environment.NewLine}{result.Error}");

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(
                   value,
                   offset,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
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

    private sealed record ProcessResult(
        int ExitCode,
        string Output,
        string Error);
}
