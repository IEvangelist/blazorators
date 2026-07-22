extern alias Server;
extern alias WebAssembly;

using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ServerDom = Server::Blazor.DOM;
using ServerInterop = Server::Microsoft.JSInterop;
using WasmDom = WebAssembly::Blazor.DOM;

namespace Blazor.WebMIDI.Tests;

public sealed class WebMIDIPackageTests
{
    [Fact]
    public void RegistrationAddsScopedCapabilityAndRuntime()
    {
        var services = new ServiceCollection();

        var returned = ServerInterop.WebMIDICapabilityServiceCollectionExtensions
            .AddWebMIDICapability(services);

        Assert.Same(services, returned);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(ServerInterop.IWebMIDICapability)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(ServerInterop.IDomProxyFactory)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void CapabilityInvokesExplicitPromiseEntryPointWithPermissionMetadata()
    {
        var request = typeof(ServerInterop.IWebMIDICapability)
            .GetMethod("RequestMIDIAccessAsync");

        Assert.NotNull(request);
        Assert.Equal(
            typeof(ValueTask<ServerDom.IMIDIAccess>),
            request.ReturnType);
        Assert.Equal(
            typeof(ServerDom.MIDIOptions),
            request.GetParameters()[0].ParameterType);
        Assert.True(ServerInterop.WebMIDICapabilityMetadata.RequiresSecureContext);
        Assert.False(ServerInterop.WebMIDICapabilityMetadata.RequiresUserActivation);
        Assert.Equal(
            ["midi", "midi-sysex"],
            ServerInterop.WebMIDICapabilityMetadata.Permissions);
        Assert.Equal(
            ["navigator.requestMIDIAccess"],
            ServerInterop.WebMIDICapabilityMetadata.FeatureDetectionPaths);
    }

    [Fact]
    public void AccessMapsAndPortsRemainLiveOwnedReferences()
    {
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            typeof(ServerDom.IMIDIAccess)));
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            typeof(ServerDom.IMIDIInputMap)));
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            typeof(ServerDom.IMIDIOutputMap)));
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            typeof(ServerDom.IMIDIPort)));

        Assert.Equal(
            typeof(ValueTask<ServerDom.IMIDIInputMap>),
            typeof(ServerDom.IMIDIAccess)
                .GetMethod("GetInputsAsync")?.ReturnType);
        Assert.Equal(
            typeof(ValueTask<ServerDom.IMIDIInput>),
            typeof(ServerDom.IMIDIInputMap)
                .GetMethod("GetAsync")?.ReturnType);
        Assert.Equal(
            typeof(ValueTask<ServerDom.IMIDIPort>),
            typeof(ServerDom.IMIDIPort).GetMethod("OpenAsync")?.ReturnType);
        Assert.Equal(
            typeof(ValueTask<ServerDom.IMIDIPort>),
            typeof(ServerDom.IMIDIPort).GetMethod("CloseAsync")?.ReturnType);

        var nullability = new NullabilityInfoContext();
        var mapLookup = typeof(ServerDom.IMIDIInputMap).GetMethod("GetAsync");
        var port = typeof(ServerDom.IMIDIConnectionEvent)
            .GetMethod("GetPortAsync");
        Assert.Equal(
            NullabilityState.Nullable,
            nullability.Create(mapLookup!.ReturnParameter)
                .GenericTypeArguments[0].ReadState);
        Assert.Equal(
            NullabilityState.Nullable,
            nullability.Create(port!.ReturnParameter)
                .GenericTypeArguments[0].ReadState);
    }

    [Fact]
    public void StateAndMessageEventsRemainTypedAndMessageDataRemainsBinary()
    {
        Assert.Equal("statechange", ServerDom.MIDIAccessEventMap.Statechange.Name);
        Assert.Equal("statechange", ServerDom.MIDIPortEventMap.Statechange.Name);
        Assert.Equal("midimessage", ServerDom.MIDIInputEventMap.Midimessage.Name);

        var data = typeof(ServerDom.IMIDIMessageEvent)
            .GetMethod("GetDataAsync");
        Assert.Equal(typeof(ValueTask<byte[]?>), data?.ReturnType);
        var accessor = data?.GetCustomAttributesData()
            .Single(attribute =>
                attribute.AttributeType.Name == "DomAccessorAttribute");
        Assert.Equal(
            (int)ServerInterop.DomTransportKind.Binary,
            Convert.ToInt32(accessor?.ConstructorArguments[2].Value));
    }

    [Fact]
    public void ServerAndWebAssemblyLogicalManifestsHaveExactParity()
    {
        using var parity = ReadManifest("host-parity.json");
        Assert.True(parity.RootElement.GetProperty("exact").GetBoolean());
        Assert.Equal(
            parity.RootElement.GetProperty("serverOperationCount").GetInt32(),
            parity.RootElement
                .GetProperty("webAssemblyOperationCount")
                .GetInt32());
        Assert.Empty(parity.RootElement.GetProperty("unexplainedDeltas")
            .EnumerateArray());

        Assert.Equal(
            typeof(ServerDom.MIDIOptions).GetProperties()
                .Select(property => property.Name).Order(),
            typeof(WasmDom.MIDIOptions).GetProperties()
                .Select(property => property.Name).Order());
    }

    [Fact]
    public void CoverageRecordsReviewedExclusionsAndNoUnsupportedMembers()
    {
        using var coverage = ReadManifest("profile-coverage.json");
        Assert.True(coverage.RootElement.GetProperty("byteIdentityVerified")
            .GetBoolean());
        Assert.Equal(
            6,
            coverage.RootElement.GetProperty("reviewedExclusions")
                .GetArrayLength());
        var accounting = coverage.RootElement.GetProperty("accounting");
        Assert.Equal(0, accounting.GetProperty("deferredMembers").GetInt32());
        Assert.Equal(0, accounting.GetProperty("failedMembers").GetInt32());

        using var server = ReadManifest("Server", "host-manifest.json");
        Assert.DoesNotContain(
            server.RootElement.GetProperty("operations").EnumerateArray(),
            operation => operation.GetProperty("kind").GetString()
                ?.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
                == true);
    }

    private static JsonDocument ReadManifest(params string[] relativePath)
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "obj",
            "Blazor.DOM.Generation",
            "release",
            "dom",
            "Profiles",
            "WebMIDI",
            Path.Combine(relativePath));
        Assert.True(File.Exists(path), $"Missing generated manifest: {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
