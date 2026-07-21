using System.Reflection;
using Blazor.DOM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blazor.Share.Tests;

public sealed class SharePackageTests
{
    [Fact]
    public void RegistrationAddsScopedCapabilityAndRuntime()
    {
        var services = new ServiceCollection();

        var returned = services.AddShareCapability();

        Assert.Same(services, returned);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IShareCapability)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IBrowser)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RootAndShareDataRemainTypedAndTransportClean()
    {
        var root = typeof(IShareCapability).GetMethod("GetNavigatorAsync");
        Assert.NotNull(root);
        Assert.Equal(typeof(ValueTask<INavigator>), root.ReturnType);

        var canShare = typeof(INavigator).GetMethod("CanShareAsync");
        var share = typeof(INavigator).GetMethod("ShareAsync");
        Assert.Equal(typeof(ValueTask<bool>), canShare?.ReturnType);
        Assert.Equal(typeof(ValueTask), share?.ReturnType);
        Assert.Equal(
            DomTransportKind.JsonValue,
            share?.GetCustomAttribute<DomOperationAttribute>()
                ?.ReturnTransport);

        Assert.NotNull(typeof(ShareData).GetProperty("Text"));
        Assert.NotNull(typeof(ShareData).GetProperty("Title"));
        Assert.NotNull(typeof(ShareData).GetProperty("Url"));
        Assert.Null(typeof(ShareData).GetProperty("Files"));
    }

    [Fact]
    public void CapabilityMetadataDeclaresActivationAndDetectionPath()
    {
        Assert.True(ShareCapabilityMetadata.RequiresSecureContext);
        Assert.True(ShareCapabilityMetadata.RequiresUserActivation);
        Assert.Equal(["web-share"], ShareCapabilityMetadata.Features);
        Assert.Equal(
            ["navigator"],
            ShareCapabilityMetadata.FeatureDetectionPaths);
    }
}
