using Blazor.DOM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blazor.Screen.Tests;

public sealed class ScreenPackageTests
{
    [Fact]
    public void RegistrationAddsScopedCapabilityAndRuntime()
    {
        var services = new ServiceCollection();

        var returned = services.AddScreenCapability();

        Assert.Same(services, returned);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IScreenCapability)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IBrowser)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RootAndOrientationProxyRemainTypedAndDisposable()
    {
        var root = typeof(IScreenCapability).GetMethod("GetScreenAsync");
        Assert.NotNull(root);
        Assert.Equal(typeof(ValueTask<IScreen>), root.ReturnType);

        var orientation = typeof(IScreen).GetMethod("GetOrientationAsync");
        Assert.NotNull(orientation);
        Assert.Equal(
            typeof(ValueTask<IScreenOrientation>),
            orientation.ReturnType);
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            typeof(IScreenOrientation)));
        Assert.Null(typeof(IScreenOrientation).GetMethod("GetOnchangeAsync"));
    }

    [Fact]
    public void ChangeEventAndCapabilityMetadataRemainTyped()
    {
        Assert.Equal("change", ScreenOrientationEventMap.Change.Name);
        Assert.Equal(
            DomTransportKind.JsReference,
            ScreenOrientationEventMap.Change.Transport.Kind);
        Assert.False(ScreenCapabilityMetadata.RequiresSecureContext);
        Assert.False(ScreenCapabilityMetadata.RequiresUserActivation);
        Assert.Equal(
            ["screen", "screen-orientation"],
            ScreenCapabilityMetadata.Features);
        Assert.Equal(
            ["window.screen"],
            ScreenCapabilityMetadata.FeatureDetectionPaths);
    }
}
