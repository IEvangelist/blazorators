using Blazor.DOM.CSharpGenerator.Anchors;
using Blazor.DOM.CSharpGenerator.Hosts;
using Blazor.DOM.CSharpGenerator.Profiles;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class InteropAnchorTests
{
    [Fact]
    public void RepositoryAnchors_DriveExhaustiveAndFocusedEntryPoints()
    {
        var root = FindRepositoryRoot();
        var anchors = InteropAnchorLoader.Load(Path.Combine(
            root,
            "src",
            "Blazor.DOM.Anchors"));

        var exhaustive = InteropAnchorLoader.CreateExhaustiveOptions(anchors);
        Assert.Equal(
            ["Document", "Navigator", "Window"],
            exhaustive.Capability.EntryPoints.Select(entryPoint => entryPoint.Name));

        var permissions = new ProfileDefinition(
            "Permissions",
            "Permissions",
            ["Permissions"],
            false,
            false,
            [],
            "Blazor.DOM",
            "Profiles/Permissions",
            EntryPoints:
            [
                new HostEntryPoint(
                    "Permissions",
                    "Permissions",
                    "navigator.permissions"),
            ]);
        var applied = InteropAnchorLoader.Apply(permissions, anchors);

        var entryPoint = Assert.Single(applied.EntryPoints!);
        Assert.Equal("navigator.permissions", entryPoint.JavaScriptPath);

        var screen = new ProfileDefinition(
            "Screen",
            "Screen",
            ["Screen"],
            false,
            false,
            [],
            "Blazor.DOM",
            "Profiles/Screen",
            EntryPoints:
            [
                new HostEntryPoint("Screen", "Screen", "window.screen"),
            ]);
        var appliedScreen = InteropAnchorLoader.Apply(screen, anchors);
        Assert.Equal(
            "window.screen",
            Assert.Single(appliedScreen.EntryPoints!).JavaScriptPath);

        foreach (var (profileName, expectedPath) in new[]
        {
            ("MediaDevices", "navigator.mediaDevices"),
            ("Notifications", "Notification"),
        })
        {
            var profile = ProfileLoader.Load(Path.Combine(
                root,
                "data",
                "Blazor.DOM.Profiles",
                $"{profileName}.profile.json"));
            var appliedProfile = InteropAnchorLoader.Apply(profile, anchors);

            Assert.Equal(
                expectedPath,
                Assert.Single(appliedProfile.EntryPoints!).JavaScriptPath);
        }
    }

    [Fact]
    public void ProfileEntryPointMismatch_FailsClosed()
    {
        var root = FindRepositoryRoot();
        var anchors = InteropAnchorLoader.Load(Path.Combine(
            root,
            "src",
            "Blazor.DOM.Anchors"));
        var profile = new ProfileDefinition(
            "WakeLock",
            "WakeLock",
            ["WakeLock"],
            true,
            false,
            [],
            "Blazor.DOM",
            "Profiles/WakeLock",
            EntryPoints:
            [
                new HostEntryPoint("WakeLock", "WakeLock", "navigator.other"),
            ]);

        Assert.Throws<InvalidDataException>(
            () => InteropAnchorLoader.Apply(profile, anchors));
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
