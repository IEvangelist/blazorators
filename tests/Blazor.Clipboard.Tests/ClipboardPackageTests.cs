using System.Reflection;
using Blazor.DOM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blazor.Clipboard.Tests;

public sealed class ClipboardPackageTests
{
    [Fact]
    public void RegistrationAddsScopedCapabilityAndRuntime()
    {
        var services = new ServiceCollection();

        var returned = services.AddClipboardCapability();

        Assert.Same(services, returned);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IClipboardCapability)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IDomProxyFactory)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RootAndTextOperationsRemainTyped()
    {
        var root = typeof(IClipboardCapability).GetMethod("GetClipboardAsync");
        Assert.NotNull(root);
        Assert.Equal(typeof(ValueTask<IClipboard>), root.ReturnType);

        var readText = typeof(IClipboard).GetMethod("ReadTextAsync");
        Assert.NotNull(readText);
        Assert.Equal(typeof(ValueTask<string>), readText.ReturnType);
        Assert.True(readText.GetCustomAttribute<DomOperationAttribute>()?.Promise);

        var writeText = typeof(IClipboard).GetMethod("WriteTextAsync");
        Assert.NotNull(writeText);
        Assert.Equal(typeof(ValueTask), writeText.ReturnType);
        Assert.Null(typeof(IClipboard).GetMethod("ReadAsync"));
        Assert.Null(typeof(IClipboard).GetMethod("WriteAsync"));
    }

    [Fact]
    public void CapabilityMetadataDeclaresSecurityAndDetectionPath()
    {
        Assert.True(ClipboardCapabilityMetadata.RequiresSecureContext);
        Assert.False(ClipboardCapabilityMetadata.RequiresUserActivation);
        Assert.Equal(
            ["clipboard-read", "clipboard-write"],
            ClipboardCapabilityMetadata.Features);
        Assert.Equal(
            ["navigator.clipboard"],
            ClipboardCapabilityMetadata.FeatureDetectionPaths);
    }
}
