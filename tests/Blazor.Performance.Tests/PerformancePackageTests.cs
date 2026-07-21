using System.Reflection;
using System.Text.Json;
using Blazor.DOM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blazor.Performance.Tests;

public sealed class PerformancePackageTests
{
    [Fact]
    public void RegistrationAddsScopedCapabilityAndRuntime()
    {
        var services = new ServiceCollection();

        var returned = services.AddPerformanceCapability();

        Assert.Same(services, returned);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IPerformanceCapability)
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
    public void CapabilityExposesPerformanceAndObserverConstructorRoots()
    {
        var performance = typeof(IPerformanceCapability)
            .GetMethod("GetPerformanceAsync");
        var observer = typeof(IPerformanceCapability)
            .GetMethod("GetPerformanceObserverAsync");

        Assert.Equal(
            typeof(ValueTask<IPerformance>),
            performance?.ReturnType);
        Assert.Equal(
            typeof(ValueTask<IPerformanceObserverFactory>),
            observer?.ReturnType);
        Assert.Equal(
            ["performance", "PerformanceObserver"],
            PerformanceCapabilityMetadata.FeatureDetectionPaths);
        Assert.False(PerformanceCapabilityMetadata.RequiresSecureContext);
    }

    [Fact]
    public void MarksMeasuresAndEntryListsRetainLiveTypedReferences()
    {
        AssertReferenceResult<IPerformanceMark>(
            typeof(IPerformance).GetMethod("MarkAsync"),
            "mark");
        AssertReferenceResult<IPerformanceMeasure>(
            typeof(IPerformance).GetMethod("MeasureAsync"),
            "measure");

        var entries = typeof(IPerformance).GetMethod("GetEntriesAsync");
        Assert.Equal(
            typeof(ValueTask<IBrowserArray<IPerformanceEntry>>),
            entries?.ReturnType);
        Assert.Equal(
            DomTransportKind.JsReference,
            entries?.GetCustomAttribute<DomOperationAttribute>()
                ?.ReturnTransport);
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            typeof(IBrowserArray<IPerformanceEntry>)));
        Assert.True(typeof(IPerformanceEntry).IsAssignableFrom(
            typeof(IPerformanceNavigationTiming)));
        Assert.True(typeof(IPerformanceEntry).IsAssignableFrom(
            typeof(IPerformanceResourceTiming)));
    }

    [Fact]
    public void ObserverCallbackOptionsBufferingAndDisposalRemainTyped()
    {
        var create = typeof(IPerformanceObserverFactory)
            .GetMethod("CreateAsync");
        Assert.NotNull(create);
        Assert.Equal(
            typeof(ValueTask<IPerformanceObserver>),
            create.ReturnType);
        var callback = create.GetParameters()[0].ParameterType;
        Assert.Equal(typeof(Func<,,>), callback.GetGenericTypeDefinition());
        Assert.Equal(
            typeof(DomBorrowedReference<IPerformanceObserverEntryList>),
            callback.GenericTypeArguments[0]);
        Assert.Equal(
            typeof(DomBorrowedReference<IPerformanceObserver>),
            callback.GenericTypeArguments[1]);
        Assert.Equal(typeof(Task), callback.GenericTypeArguments[2]);
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            typeof(IPerformanceObserver)));

        var observe = typeof(IPerformanceObserver).GetMethod("ObserveAsync");
        Assert.Equal(
            typeof(PerformanceObserverInit),
            observe?.GetParameters()[0].ParameterType);
        var options = new PerformanceObserverInit
        {
            Buffered = true,
            EntryTypes = ["mark", "measure"],
        };
        Assert.True(options.Buffered);
        Assert.Equal(["mark", "measure"], options.EntryTypes);

        var records = typeof(IPerformanceObserver).GetMethod("TakeRecordsAsync");
        Assert.Equal(
            typeof(ValueTask<IBrowserArray<IPerformanceEntry>>),
            records?.ReturnType);
        Assert.Equal(
            "resourcetimingbufferfull",
            PerformanceEventMap.Resourcetimingbufferfull.Name);
    }

    [Fact]
    public void GeneratedManifestsHaveExactParityAndCompleteCoverage()
    {
        using var parity = ReadManifest("host-parity.json");
        Assert.True(parity.RootElement.GetProperty("exact").GetBoolean());
        Assert.Equal(
            126,
            parity.RootElement.GetProperty("serverOperationCount").GetInt32());
        Assert.Equal(
            126,
            parity.RootElement
                .GetProperty("webAssemblyOperationCount")
                .GetInt32());
        Assert.Empty(parity.RootElement.GetProperty("unexplainedDeltas")
            .EnumerateArray());

        using var coverage = ReadManifest("profile-coverage.json");
        Assert.Equal(
            28,
            coverage.RootElement.GetProperty("closureSize").GetInt32());
        Assert.Equal(
            27,
            coverage.RootElement.GetProperty("includedSymbolCount").GetInt32());
        var accounting = coverage.RootElement.GetProperty("accounting");
        Assert.Equal(0, accounting.GetProperty("deferredMembers").GetInt32());
        Assert.Equal(0, accounting.GetProperty("failedMembers").GetInt32());

        using var server = ReadManifest("Server", "host-manifest.json");
        Assert.Equal(
            126,
            server.RootElement.GetProperty("operations").GetArrayLength());
        Assert.Contains(
            server.RootElement.GetProperty("operations").EnumerateArray(),
            operation =>
                operation.GetProperty("kind").GetString()
                    == "constructor-callback"
                && operation.GetProperty("javaScriptName").GetString()
                    == "PerformanceObserver");
        Assert.DoesNotContain(
            server.RootElement.GetProperty("operations").EnumerateArray(),
            operation => operation.GetProperty("kind").GetString()
                ?.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
                == true);
    }

    private static void AssertReferenceResult<TResult>(
        MethodInfo? method,
        string javaScriptName)
    {
        Assert.NotNull(method);
        Assert.Equal(typeof(ValueTask<TResult>), method.ReturnType);
        var operation = method.GetCustomAttribute<DomOperationAttribute>();
        Assert.Equal(javaScriptName, operation?.JavaScriptName);
        Assert.Equal(DomTransportKind.JsReference, operation?.ReturnTransport);
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(TResult)));
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
            "Performance",
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
