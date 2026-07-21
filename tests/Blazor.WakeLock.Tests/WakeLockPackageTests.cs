using System.Reflection;
using Blazor.DOM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blazor.WakeLock.Tests;

public sealed class WakeLockPackageTests
{
    [Fact]
    public void Registration_AddsScopedCapabilityAndRuntime()
    {
        var services = new ServiceCollection();

        var returned = services.AddWakeLockCapability();

        Assert.Same(services, returned);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IWakeLockCapability)
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
    public void RootAndReturnedProxyContractsRemainTypedAndDisposable()
    {
        var root = typeof(IWakeLockCapability).GetMethod("GetWakeLockAsync");
        Assert.NotNull(root);
        Assert.Equal(
            typeof(ValueTask<IWakeLock>),
            root.ReturnType);

        var request = typeof(IWakeLock).GetMethod("RequestAsync");
        Assert.NotNull(request);
        Assert.Equal(
            typeof(ValueTask<IWakeLockSentinel>),
            request.ReturnType);
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            typeof(IWakeLockSentinel)));
        Assert.True(typeof(IDomDispatchProxy).IsAssignableFrom(
            typeof(IWakeLockSentinel)));
    }

    [Fact]
    public void EventAndDisposalOperationsRetainSemanticMetadata()
    {
        Assert.Equal(
            "release",
            WakeLockSentinelEventMap.Release.Name);
        Assert.Equal(
            DomTransportKind.JsReference,
            WakeLockSentinelEventMap.Release.Transport.Kind);

        var release = typeof(IWakeLockSentinel).GetMethod("ReleaseAsync");
        var operation = release?.GetCustomAttribute<DomOperationAttribute>();
        Assert.NotNull(operation);
        Assert.Equal("release", operation.JavaScriptName);
        Assert.True(operation.Promise);
        Assert.DoesNotContain(
            typeof(IWakeLockSentinel).GetMethods(),
            method => method.GetCustomAttributes<DomAccessorAttribute>()
                .Any(attribute =>
                    attribute.TransportKind == DomTransportKind.Unsupported));
    }

    [Fact]
    public void CapabilityMetadataDeclaresSecurityAndDetectionPath()
    {
        Assert.True(WakeLockCapabilityMetadata.RequiresSecureContext);
        Assert.False(WakeLockCapabilityMetadata.RequiresUserActivation);
        Assert.Equal(
            ["screen-wake-lock"],
            WakeLockCapabilityMetadata.Features);
        Assert.Equal(
            ["navigator.wakeLock"],
            WakeLockCapabilityMetadata.FeatureDetectionPaths);
    }
}
