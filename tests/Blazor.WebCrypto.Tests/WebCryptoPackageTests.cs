using System.Collections;
using System.Reflection;
using Blazor.DOM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blazor.WebCrypto.Tests;

public sealed class WebCryptoPackageTests
{
    [Fact]
    public void RegistrationAddsScopedCapabilityAndRuntime()
    {
        var services = new ServiceCollection();

        var returned = services.AddWebCryptoCapability();

        Assert.Same(services, returned);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IWebCryptoCapability)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IDomProxyFactory)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void CapabilityUsesExplicitCryptoRootsAndSecurityMetadata()
    {
        Assert.Equal(
            typeof(ValueTask<ICrypto>),
            typeof(IWebCryptoCapability).GetMethod("GetCryptoAsync")?.ReturnType);
        Assert.Equal(
            typeof(ValueTask<ISubtleCrypto>),
            typeof(IWebCryptoCapability).GetMethod("GetSubtleCryptoAsync")?.ReturnType);
        Assert.True(WebCryptoCapabilityMetadata.RequiresSecureContext);
        Assert.False(WebCryptoCapabilityMetadata.RequiresUserActivation);
        Assert.Equal(["web-crypto"], WebCryptoCapabilityMetadata.Features);
        Assert.Equal(
            ["crypto", "crypto.subtle"],
            WebCryptoCapabilityMetadata.FeatureDetectionPaths);
    }

    [Fact]
    public void RandomValuesAndBufferSourcesRemainBinaryAndTyped()
    {
        var random = typeof(ICrypto).GetMethod("GetRandomValuesAsync");
        Assert.NotNull(random);
        Assert.True(random.IsGenericMethodDefinition);
        Assert.Equal(typeof(IList), random.GetGenericArguments()[0]
            .GetGenericParameterConstraints().Single());
        Assert.Equal(
            DomTransportKind.Binary,
            random.GetCustomAttribute<DomOperationAttribute>()?.ReturnTransport);

        var bytes = new byte[] { 1, 2, 3 };
        var source = BufferSource.FromArrayBuffer(bytes);
        Assert.True(source.IsArrayBuffer);
        Assert.Same(bytes, source.GetArrayBuffer());
        Assert.Equal(
            typeof(BufferSource),
            typeof(AesGcmParams).GetProperty("Iv")?.PropertyType);
        Assert.NotNull(typeof(AesGcmParams).GetCustomAttribute<DomJsonValueAttribute>());
    }

    [Fact]
    public void SubtleCryptoPromiseOperationsPreserveBinaryAndCryptoKeyProxies()
    {
        var encrypt = typeof(ISubtleCrypto).GetMethod("EncryptAsync");
        var import = typeof(ISubtleCrypto).GetMethods()
            .Single(method =>
                method.Name == "ImportKeyAsync"
                && method.GetParameters()[1].ParameterType == typeof(BufferSource));
        var derive = typeof(ISubtleCrypto).GetMethod("DeriveKeyAsync");

        Assert.Equal(typeof(ValueTask<byte[]>), encrypt?.ReturnType);
        AssertOperation(encrypt, DomTransportKind.Binary);
        Assert.Equal(typeof(ValueTask<ICryptoKey>), import.ReturnType);
        AssertOperation(import, DomTransportKind.JsReference);
        Assert.Equal(typeof(ValueTask<ICryptoKey>), derive?.ReturnType);
        AssertOperation(derive, DomTransportKind.JsReference);
        Assert.True(typeof(IDomDispatchProxy).IsAssignableFrom(typeof(ICryptoKey)));
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(ICryptoKey)));
    }

    [Fact]
    public void SubtleCryptoExposesReviewedTypedOperationSet()
    {
        string[] expected =
        [
            "DecryptAsync",
            "DeriveBitsAsync",
            "DeriveKeyAsync",
            "DigestAsync",
            "EncryptAsync",
            "ExportKeyAsync",
            "GenerateKeyAsync",
            "ImportKeyAsync",
            "SignAsync",
            "UnwrapKeyAsync",
            "VerifyAsync",
            "WrapKeyAsync",
        ];
        var methods = typeof(ISubtleCrypto).GetMethods()
            .Where(method => method.GetCustomAttribute<DomOperationAttribute>() is not null)
            .ToList();

        Assert.All(expected, name => Assert.Contains(methods, method => method.Name == name));
        Assert.All(
            methods,
            method => Assert.True(
                method.GetCustomAttribute<DomOperationAttribute>()?.Promise));
        Assert.DoesNotContain(
            methods,
            method => method.GetCustomAttribute<DomOperationAttribute>()
                ?.ReturnTransport == DomTransportKind.Unsupported);
    }

    private static void AssertOperation(
        MethodInfo? method,
        DomTransportKind returnTransport)
    {
        var operation = method?.GetCustomAttribute<DomOperationAttribute>();
        Assert.NotNull(operation);
        Assert.Equal(returnTransport, operation.ReturnTransport);
        Assert.True(operation.Promise);
    }
}
