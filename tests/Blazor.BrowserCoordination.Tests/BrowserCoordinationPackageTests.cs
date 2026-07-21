using System.Reflection;
using System.Text.Json;
using Blazor.DOM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blazor.BrowserCoordination.Tests;

public sealed class BrowserCoordinationPackageTests
{
    [Fact]
    public void RegistrationAddsScopedCapabilityAndRuntime()
    {
        var services = new ServiceCollection();

        var returned = services.AddBrowserCoordinationCapability();

        Assert.Same(services, returned);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IBrowserCoordinationCapability)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IDomProxyFactory)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IBrowser)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void CapabilityExposesOnlyReviewedEntryPoints()
    {
        var methods = typeof(IBrowserCoordinationCapability)
            .GetMethods()
            .ToDictionary(method => method.Name);

        Assert.Equal(
            [
                "GetBroadcastChannelAsync",
                "GetDocumentAsync",
                "GetLockManagerAsync",
            ],
            methods.Keys.Order());
        Assert.Equal(
            typeof(ValueTask<IBroadcastChannelFactory>),
            methods["GetBroadcastChannelAsync"].ReturnType);
        Assert.Equal(
            typeof(ValueTask<IDocument>),
            methods["GetDocumentAsync"].ReturnType);
        Assert.Equal(
            typeof(ValueTask<ILockManager>),
            methods["GetLockManagerAsync"].ReturnType);
        Assert.Equal(
            ["BroadcastChannel", "document", "navigator.locks"],
            BrowserCoordinationCapabilityMetadata.FeatureDetectionPaths);
        Assert.True(BrowserCoordinationCapabilityMetadata.RequiresSecureContext);
    }

    [Fact]
    public void ConstructorLocksAndVisibilityRetainTypedTransport()
    {
        var create = typeof(IBroadcastChannelFactory).GetMethod("CreateAsync");
        Assert.NotNull(create);
        Assert.Equal(
            typeof(ValueTask<IBroadcastChannel>),
            create.ReturnType);
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            typeof(IBroadcastChannel)));

        var request = Assert.Single(
            typeof(ILockManager).GetMethods(),
            method => method.Name == "RequestAsync");
        Assert.True(request.IsGenericMethodDefinition);
        Assert.Equal("request", request.GetCustomAttribute<DomOperationAttribute>()
            ?.JavaScriptName);
        var callback = request.GetParameters()[1].ParameterType;
        Assert.Equal(typeof(Func<,>), callback.GetGenericTypeDefinition());
        Assert.Equal(
            typeof(DomBorrowedReference<ILock>),
            callback.GenericTypeArguments[0]);

        var hidden = typeof(IDocument).GetMethod("GetHiddenAsync");
        var visibility = typeof(IDocument).GetMethod("GetVisibilityStateAsync");
        Assert.Equal(typeof(ValueTask<bool>), hidden?.ReturnType);
        Assert.Equal(
            typeof(ValueTask<DocumentVisibilityState>),
            visibility?.ReturnType);
    }

    [Fact]
    public void EventNamesAndDisposalOperationsRemainExact()
    {
        Assert.Equal("message", BroadcastChannelEventMap.Message.Name);
        Assert.Equal("messageerror", BroadcastChannelEventMap.Messageerror.Name);
        Assert.Equal(
            DomTransportKind.JsReference,
            BroadcastChannelEventMap.Message.Transport.Kind);
        Assert.Equal(
            "visibilitychange",
            DocumentEventMap.Visibilitychange.Name);

        var close = typeof(IBroadcastChannel).GetMethod("CloseAsync");
        var operation = close?.GetCustomAttribute<DomOperationAttribute>();
        Assert.Equal("close", operation?.JavaScriptName);
        Assert.False(operation?.Promise);
        Assert.Null(typeof(IBroadcastChannel).GetProperty("Onmessage"));
        Assert.Null(typeof(IDocument).GetProperty("Onvisibilitychange"));
    }

    [Fact]
    public void GeneratedManifestsHaveExactParityAndCompleteCoverage()
    {
        using var parity = ReadManifest("host-parity.json");
        Assert.True(parity.RootElement.GetProperty("exact").GetBoolean());
        Assert.Equal(
            64,
            parity.RootElement.GetProperty("serverOperationCount").GetInt32());
        Assert.Equal(
            64,
            parity.RootElement
                .GetProperty("webAssemblyOperationCount")
                .GetInt32());
        Assert.Empty(parity.RootElement.GetProperty("unexplainedDeltas")
            .EnumerateArray());

        using var coverage = ReadManifest("profile-coverage.json");
        Assert.Equal(
            30,
            coverage.RootElement.GetProperty("closureSize").GetInt32());
        Assert.Equal(
            29,
            coverage.RootElement.GetProperty("includedSymbolCount").GetInt32());
        var accounting = coverage.RootElement.GetProperty("accounting");
        Assert.Equal(0, accounting.GetProperty("deferredMembers").GetInt32());
        Assert.Equal(0, accounting.GetProperty("failedMembers").GetInt32());

        using var server = ReadManifest("Server", "host-manifest.json");
        Assert.Equal(
            64,
            server.RootElement.GetProperty("operations").GetArrayLength());
        Assert.DoesNotContain(
            server.RootElement.GetProperty("operations").EnumerateArray(),
            operation => operation.GetProperty("kind").GetString()
                ?.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
                == true);
    }

    private static JsonDocument ReadManifest(params string[] relativePath)
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(
            root,
            "artifacts",
            "obj",
            "Blazor.DOM.Generation",
            "release",
            "dom",
            "Profiles",
            "BrowserCoordination",
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
