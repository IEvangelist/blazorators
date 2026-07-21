using System.Reflection;
using Blazor.DOM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blazor.StorageManagement.Tests;

public sealed class StorageManagementPackageTests
{
    [Fact]
    public void RegistrationAddsScopedCapabilityAndRuntime()
    {
        var services = new ServiceCollection();

        var returned = services.AddStorageManagementCapability();

        Assert.Same(services, returned);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IStorageManagementCapability)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IDomProxyFactory)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RootAndDirectoryProxyRemainTypedAndDisposable()
    {
        var root = typeof(IStorageManagementCapability)
            .GetMethod("GetStorageManagerAsync");
        Assert.NotNull(root);
        Assert.Equal(typeof(ValueTask<IStorageManager>), root.ReturnType);

        var getDirectory = typeof(IStorageManager)
            .GetMethod("GetDirectoryAsync");
        Assert.NotNull(getDirectory);
        Assert.Equal(
            typeof(ValueTask<IFileSystemDirectoryHandle>),
            getDirectory.ReturnType);
        Assert.Equal(
            DomTransportKind.JsReference,
            getDirectory.GetCustomAttribute<DomOperationAttribute>()
                ?.ReturnTransport);
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            typeof(IFileSystemDirectoryHandle)));
    }

    [Fact]
    public void CapabilityMetadataDeclaresSecurityAndDetectionPath()
    {
        Assert.True(StorageManagementCapabilityMetadata.RequiresSecureContext);
        Assert.False(
            StorageManagementCapabilityMetadata.RequiresUserActivation);
        Assert.Equal(
            ["storage", "origin-private-file-system"],
            StorageManagementCapabilityMetadata.Features);
        Assert.Equal(
            ["navigator.storage"],
            StorageManagementCapabilityMetadata.FeatureDetectionPaths);
    }
}
