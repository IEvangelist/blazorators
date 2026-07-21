// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.DOM.Tests.Fakes;

namespace Blazor.DOM.Tests;

/// <summary>
/// Tests for <see cref="DomProxyFactory"/> registration, creation, and error paths.
/// </summary>
public sealed class DomProxyFactoryTests
{
    private static (DomProxyFactory Factory, IDomRuntime Runtime) CreateFactory()
    {
        var module    = new FakeJSObjectReference();
        var jsRuntime = new FakeJSRuntime(module);
        var runtime   = new ServerDomRuntime(jsRuntime);
        return (new DomProxyFactory(runtime), runtime);
    }

    [Fact]
    public void Create_throws_when_type_not_registered()
    {
        var (factory, _) = CreateFactory();
        var jsRef = new FakeJSObjectReference();

        var ex = Assert.Throws<InvalidOperationException>(
            () => factory.Create<TestDomProxy>(jsRef));

        Assert.Contains(nameof(TestDomProxy), ex.Message);
    }

    [Fact]
    public void Create_returns_registered_proxy()
    {
        var (factory, runtime) = CreateFactory();
        factory.Register<TestDomProxy>(
            (r, rt, f) => new TestDomProxy(r, rt, f));

        var jsRef  = new FakeJSObjectReference();
        var proxy  = factory.Create<TestDomProxy>(jsRef);

        Assert.NotNull(proxy);
        Assert.Same(jsRef, proxy.Reference);
        Assert.Same(runtime, proxy.RuntimeExposed);
        Assert.Same(factory, proxy.FactoryExposed);
    }

    [Fact]
    public void Create_by_contract_type_returns_registered_proxy()
    {
        var (factory, _) = CreateFactory();
        factory.Register(
            typeof(ITestDomContract),
            (reference, runtime, owner) =>
                new ContractDomProxy(reference, runtime, owner));

        var proxy = factory.Create(
            typeof(ITestDomContract),
            new FakeJSObjectReference());

        Assert.IsType<ContractDomProxy>(proxy);
        Assert.IsAssignableFrom<ITestDomContract>(proxy);
    }

    [Fact]
    public void Create_constructed_generic_uses_open_registration()
    {
        var (factory, _) = CreateFactory();
        factory.RegisterOpenGeneric(
            typeof(IGenericDomContract<>),
            typeof(GenericDomProxy<>));

        var proxy = factory.Create(
            typeof(IGenericDomContract<string>),
            new FakeJSObjectReference());

        Assert.IsType<GenericDomProxy<string>>(proxy);
        Assert.IsAssignableFrom<IGenericDomContract<string>>(proxy);
    }

    [Fact]
    public void Register_overwrites_previous_registration_for_same_type()
    {
        var (factory, _) = CreateFactory();
        var firstRef  = new FakeJSObjectReference();
        var secondRef = new FakeJSObjectReference();

        factory.Register<TestDomProxy>((r, rt, f) => new TestDomProxy(firstRef,  rt, f));
        factory.Register<TestDomProxy>((r, rt, f) => new TestDomProxy(secondRef, rt, f));

        var jsRef = new FakeJSObjectReference();
        var proxy = factory.Create<TestDomProxy>(jsRef);

        Assert.Same(secondRef, proxy.Reference);
    }

    [Fact]
    public void Create_throws_for_null_reference()
    {
        var (factory, _) = CreateFactory();
        factory.Register<TestDomProxy>((r, rt, f) => new TestDomProxy(r, rt, f));

        Assert.Throws<ArgumentNullException>(() => factory.Create<TestDomProxy>(null!));
    }

    [Fact]
    public async Task DI_registration_AddBlazorDOM_registers_expected_services()
    {
        var services = new ServiceCollection();
        services.AddScoped<IJSRuntime>(_ => new FakeJSRuntime());
        services.AddBlazorDOM();

        await using var provider = services.BuildServiceProvider();
        await using var scope    = provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IDomRuntime>());
        Assert.NotNull(sp.GetRequiredService<IDomProxyFactory>());
        Assert.NotNull(sp.GetRequiredService<IBrowser>());
    }

    // ── Exposed-accessor proxy ────────────────────────────────────────────

    internal sealed class TestDomProxy(
        IJSObjectReference reference,
        IDomRuntime runtime,
        IDomProxyFactory factory) : DomProxyBase(reference, runtime, factory)
    {
        // Expose for test assertions
        public IDomRuntime     RuntimeExposed => Runtime;
        public IDomProxyFactory FactoryExposed => Factory;
    }

    internal interface ITestDomContract : IDomProxy;

    internal sealed class ContractDomProxy(
        IJSObjectReference reference,
        IDomRuntime runtime,
        IDomProxyFactory factory)
        : DomProxyBase(reference, runtime, factory), ITestDomContract;

    internal interface IGenericDomContract<T> : IDomProxy;

    internal sealed class GenericDomProxy<T>(
        IJSObjectReference reference,
        IDomRuntime runtime,
        IDomProxyFactory factory)
        : DomProxyBase(reference, runtime, factory), IGenericDomContract<T>;
}
