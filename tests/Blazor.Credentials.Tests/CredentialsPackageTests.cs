using System.Reflection;
using Blazor.DOM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blazor.Credentials.Tests;

public sealed class CredentialsPackageTests
{
    [Fact]
    public void RegistrationAddsScopedCapabilityAndRuntime()
    {
        var services = new ServiceCollection();

        var returned = services.AddCredentialsCapability();

        Assert.Same(services, returned);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(ICredentialsCapability)
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
    public void CapabilityUsesExplicitCredentialsRootAndSecurityMetadata()
    {
        var root = typeof(ICredentialsCapability)
            .GetMethod("GetCredentialsContainerAsync");

        Assert.NotNull(root);
        Assert.Equal(
            typeof(ValueTask<ICredentialsContainer>),
            root.ReturnType);
        Assert.True(CredentialsCapabilityMetadata.RequiresSecureContext);
        Assert.True(CredentialsCapabilityMetadata.RequiresUserActivation);
        Assert.Equal(
            ["credential-management", "webauthn"],
            CredentialsCapabilityMetadata.Features);
        Assert.Equal(
            ["navigator.credentials"],
            CredentialsCapabilityMetadata.FeatureDetectionPaths);
    }

    [Fact]
    public void CredentialCallsPreservePromiseOptionsAndLiveReferences()
    {
        var create = typeof(ICredentialsContainer).GetMethod("CreateAsync");
        var get = typeof(ICredentialsContainer).GetMethod("GetAsync");

        Assert.NotNull(create);
        Assert.NotNull(get);
        Assert.Equal(typeof(ValueTask<ICredential>), create.ReturnType);
        Assert.Equal(typeof(CredentialCreationOptions), create.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CredentialRequestOptions), get.GetParameters()[0].ParameterType);
        AssertOperation(create, "create", DomTransportKind.JsReference);
        AssertOperation(get, "get", DomTransportKind.JsReference);
        Assert.True(typeof(IDomDispatchProxy).IsAssignableFrom(typeof(IPublicKeyCredential)));
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IPublicKeyCredential)));
    }

    [Fact]
    public void WebAuthnContractsKeepBinaryOptionsAndResponseProxiesTyped()
    {
        Assert.Equal(
            typeof(PublicKeyCredentialCreationOptions),
            typeof(CredentialCreationOptions).GetProperty("PublicKey")?.PropertyType);
        Assert.Equal(
            typeof(BufferSource),
            typeof(PublicKeyCredentialCreationOptions).GetProperty("Challenge")?.PropertyType);
        Assert.Equal(
            typeof(BufferSource),
            typeof(PublicKeyCredentialUserEntity).GetProperty("Id")?.PropertyType);
        Assert.NotNull(
            typeof(PublicKeyCredentialCreationOptions)
                .GetCustomAttribute<DomJsonValueAttribute>());

        var rawId = typeof(IPublicKeyCredential).GetMethod("GetRawIdAsync");
        var response = typeof(IPublicKeyCredential).GetMethod("GetResponseAsync");
        Assert.Equal(typeof(ValueTask<byte[]>), rawId?.ReturnType);
        Assert.Equal(
            DomTransportKind.Binary,
            rawId?.GetCustomAttribute<DomAccessorAttribute>()?.TransportKind);
        Assert.Equal(
            typeof(ValueTask<IAuthenticatorResponse>),
            response?.ReturnType);
        Assert.Equal(
            DomTransportKind.JsReference,
            response?.GetCustomAttribute<DomAccessorAttribute>()?.TransportKind);

        var attestation = typeof(IAuthenticatorAttestationResponse)
            .GetMethod("GetAttestationObjectAsync");
        var signature = typeof(IAuthenticatorAssertionResponse)
            .GetMethod("GetSignatureAsync");
        Assert.Equal(typeof(ValueTask<byte[]>), attestation?.ReturnType);
        Assert.Equal(typeof(ValueTask<byte[]>), signature?.ReturnType);
    }

    private static void AssertOperation(
        MethodInfo method,
        string javaScriptName,
        DomTransportKind returnTransport)
    {
        var operation = method.GetCustomAttribute<DomOperationAttribute>();
        Assert.NotNull(operation);
        Assert.Equal(javaScriptName, operation.JavaScriptName);
        Assert.Equal(returnTransport, operation.ReturnTransport);
        Assert.True(operation.Promise);
    }
}
