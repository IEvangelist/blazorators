using System.Reflection;
using Blazor.DOM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blazor.OfflineStorage.Tests;

public sealed class OfflineStoragePackageTests
{
    [Fact]
    public void RegistrationAddsScopedCapabilityBrowserAndFactory()
    {
        var services = new ServiceCollection();

        var returned = services.AddOfflineStorageCapability();

        Assert.Same(services, returned);
        AssertScoped<IOfflineStorageCapability>(services);
        AssertScoped<IBrowser>(services);
        AssertScoped<IDomProxyFactory>(services);
    }

    [Fact]
    public void WindowRootsAndCapabilityMetadataAreExplicit()
    {
        Assert.Equal(
            typeof(ValueTask<ICacheStorage>),
            typeof(IOfflineStorageCapability)
                .GetMethod("GetCacheStorageAsync")?.ReturnType);
        Assert.Equal(
            typeof(ValueTask<IIDBFactory>),
            typeof(IOfflineStorageCapability)
                .GetMethod("GetIDBFactoryAsync")?.ReturnType);
        Assert.Equal(
            ["caches", "indexedDB"],
            OfflineStorageCapabilityMetadata.FeatureDetectionPaths);
        Assert.Equal(
            ["cache-storage", "indexed-db", "structured-clone"],
            OfflineStorageCapabilityMetadata.Features);
        Assert.True(OfflineStorageCapabilityMetadata.RequiresSecureContext);
        Assert.False(OfflineStorageCapabilityMetadata.RequiresUserActivation);
    }

    [Fact]
    public void CachePromisesReturnOwnedResponseRequestAndArrayProxies()
    {
        AssertPromiseReference<ICacheStorage, ICache>("OpenAsync");
        AssertPromiseReference<ICacheStorage, IResponse>("MatchAsync", nullable: true);
        AssertPromiseReference<ICache, IResponse>("MatchAsync", nullable: true);
        AssertPromiseReference<ICache, IReadOnlyBrowserArray<IRequest>>("KeysAsync");
        AssertPromiseReference<ICache, IReadOnlyBrowserArray<IResponse>>(
            "MatchAllAsync");

        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(ICache)));
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IRequest)));
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IResponse)));
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            typeof(IReadOnlyBrowserArray<IResponse>)));
    }

    [Fact]
    public void IndexedDbRequestsEventsErrorsAndOwnershipRemainTyped()
    {
        var result = RequireMethod(typeof(IIDBRequest<object>), "GetResultAsync");
        Assert.Equal(typeof(ValueTask<object>), result.ReturnType);
        Assert.Equal(
            DomTransportKind.Inferred,
            result.GetCustomAttribute<DomAccessorAttribute>()?.TransportKind);

        var error = RequireMethod(typeof(IIDBRequest<object>), "GetErrorAsync");
        var errorMetadata = error.GetCustomAttribute<DomAccessorAttribute>();
        Assert.Equal(DomTransportKind.JsReference, errorMetadata?.TransportKind);
        Assert.True(errorMetadata?.Nullable);

        Assert.Equal("success", IDBRequestEventMap.Success.Name);
        Assert.IsType<DomEventDescriptor<IEvent>>(IDBRequestEventMap.Success);
        Assert.Equal(
            "upgradeneeded",
            IDBOpenDBRequestEventMap.Upgradeneeded.Name);
        Assert.IsType<DomEventDescriptor<IIDBVersionChangeEvent>>(
            IDBOpenDBRequestEventMap.Upgradeneeded);
        Assert.Contains(
            typeof(IIDBRequest<object>).GetMethods(),
            method => method.Name == "RemoveEventListenerAsync");

        AssertReferenceResult<IIDBTransaction, IIDBObjectStore>("ObjectStoreAsync");
        AssertReferenceResult<IIDBCursor, IIDBRequest<object>>("GetRequestAsync");
        AssertReferenceResult<IIDBCursor, IIDBRequest<BrowserUndefined>>(
            "DeleteAsync");
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            typeof(IIDBTransaction)));
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            typeof(IIDBCursorWithValue)));
    }

    [Fact]
    public void StructuredCloneBoundariesAndReviewedExclusionsRemainExact()
    {
        AssertInferredObjectAccessor<IIDBCursorWithValue>("GetValueAsync");
        AssertInferredObjectAccessor<IIDBKeyRange>("GetLowerAsync");
        AssertInferredObjectAccessor<IIDBKeyRange>("GetUpperAsync");

        Assert.Null(typeof(IIDBCursor).GetMethod("GetKeyAsync"));
        Assert.Null(typeof(IIDBCursor).GetMethod("GetPrimaryKeyAsync"));
        Assert.Null(typeof(IIDBRequest<object>).GetMethod("GetSourceAsync"));
    }

    private static void AssertPromiseReference<TContract, TResult>(
        string name,
        bool nullable = false)
    {
        var method = typeof(TContract)
            .GetMethods()
            .Single(candidate => candidate.Name == name);
        Assert.Equal(typeof(ValueTask<TResult>), method.ReturnType);
        var operation = method.GetCustomAttribute<DomOperationAttribute>();
        Assert.NotNull(operation);
        Assert.True(operation.Promise);
        Assert.Equal(DomTransportKind.JsReference, operation.ReturnTransport);
        Assert.Equal(nullable, operation.Nullable);
    }

    private static void AssertReferenceResult<TContract, TResult>(string name)
    {
        var method = RequireMethod(typeof(TContract), name);
        Assert.Equal(typeof(ValueTask<TResult>), method.ReturnType);
        Assert.Equal(
            DomTransportKind.JsReference,
            method.GetCustomAttribute<DomOperationAttribute>()?.ReturnTransport
                ?? method.GetCustomAttribute<DomAccessorAttribute>()?.TransportKind);
    }

    private static void AssertInferredObjectAccessor<TContract>(string name)
    {
        var method = RequireMethod(typeof(TContract), name);
        Assert.Equal(typeof(ValueTask<object>), method.ReturnType);
        Assert.Equal(
            DomTransportKind.Inferred,
            method.GetCustomAttribute<DomAccessorAttribute>()?.TransportKind);
    }

    private static MethodInfo RequireMethod(Type type, string name) =>
        type.GetMethod(name)
        ?? throw new Xunit.Sdk.XunitException(
            $"Expected method '{type.FullName}.{name}' was not generated.");

    private static void AssertScoped<TService>(IServiceCollection services) =>
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(TService)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
}
