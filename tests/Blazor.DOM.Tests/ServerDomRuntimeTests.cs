// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.DOM.Tests.Fakes;

namespace Blazor.DOM.Tests;

/// <summary>
/// Tests for <see cref="ServerDomRuntime"/> async dispatch paths.
/// </summary>
public sealed class ServerDomRuntimeTests
{
    private static (ServerDomRuntime Runtime, FakeJSObjectReference Module) CreateRuntime()
    {
        var module    = new FakeJSObjectReference();
        var jsRuntime = new FakeJSRuntime(module);
        return (new ServerDomRuntime(jsRuntime), module);
    }

    [Fact]
    public async Task GetPropertyAsync_forwards_identifier_and_args()
    {
        var (runtime, module) = CreateRuntime();
        var target = new FakeJSObjectReference();
        module.ReturnValues["getProperty"] = "hello";

        var result = await runtime.GetPropertyAsync<string>(target, "title");

        Assert.Equal("hello", result);
        var call = Assert.Single(module.Invocations, i => i.Identifier == "getProperty");
        Assert.Equal(target, call.Args![0]);
        Assert.Equal("title", call.Args![1]);
    }

    [Fact]
    public async Task SetPropertyAsync_calls_setProperty_on_module()
    {
        var (runtime, module) = CreateRuntime();
        var target = new FakeJSObjectReference();

        await runtime.SetPropertyAsync(target, "innerHTML", "<p>hi</p>");

        var call = Assert.Single(module.Invocations, i => i.Identifier == "setProperty");
        Assert.Equal(target,      call.Args![0]);
        Assert.Equal("innerHTML", call.Args![1]);
        Assert.Equal("<p>hi</p>", call.Args![2]);
    }

    [Fact]
    public async Task InvokeMethodAsync_passes_args_array()
    {
        var (runtime, module) = CreateRuntime();
        var target = new FakeJSObjectReference();
        module.ReturnValues["invokeMethod"] = 42;

        var result = await runtime.InvokeMethodAsync<int>(target, "getBoundingClientRect", ["arg1"]);

        Assert.Equal(42, result);
        var call = Assert.Single(module.Invocations, i => i.Identifier == "invokeMethod");
        Assert.Equal(target,               call.Args![0]);
        Assert.Equal("getBoundingClientRect", call.Args![1]);
    }

    [Fact]
    public async Task InvokeMethodVoidAsync_calls_invokeMethod_on_module()
    {
        var (runtime, module) = CreateRuntime();
        var target = new FakeJSObjectReference();

        await runtime.InvokeMethodVoidAsync(target, "click", null);

        var call = Assert.Single(module.Invocations, i => i.Identifier == "invokeMethod");
        Assert.Equal("click", call.Args![1]);
    }

    [Fact]
    public async Task InvokeMethodRefAsync_returns_IJSObjectReference()
    {
        var (runtime, module) = CreateRuntime();
        var target  = new FakeJSObjectReference();
        var retRef  = new FakeJSObjectReference();
        module.ReturnValues["invokeMethod"] = retRef;

        var result = await runtime.InvokeMethodRefAsync(target, "querySelector", ["#id"]);

        Assert.Same(retRef, result);
    }

    [Fact]
    public async Task GetGlobalAsync_calls_getGlobal_with_path()
    {
        var (runtime, module) = CreateRuntime();
        var winRef = new FakeJSObjectReference();
        module.ReturnValues["getGlobal"] = winRef;

        var result = await runtime.GetGlobalAsync("window");

        Assert.Same(winRef, result);
        var call = Assert.Single(module.Invocations, i => i.Identifier == "getGlobal");
        Assert.Equal("window", call.Args![0]);
    }

    [Fact]
    public async Task IsGlobalAvailableAsync_calls_hasGlobal_with_path()
    {
        var (runtime, module) = CreateRuntime();
        module.ReturnValues["hasGlobal"] = true;

        var result = await runtime.IsGlobalAvailableAsync(
            "navigator.wakeLock");

        Assert.True(result);
        var call = Assert.Single(
            module.Invocations,
            invocation => invocation.Identifier == "hasGlobal");
        Assert.Equal("navigator.wakeLock", call.Args![0]);
    }

    [Fact]
    public async Task ConstructAsync_calls_construct_with_path_and_args()
    {
        var (runtime, module) = CreateRuntime();
        var instance = new FakeJSObjectReference();
        module.ReturnValues["construct"] = instance;

        var result = await runtime.ConstructAsync("URL", ["https://example.com"]);

        Assert.Same(instance, result);
        var call = Assert.Single(module.Invocations, i => i.Identifier == "construct");
        Assert.Equal("URL", call.Args![0]);
    }

    [Fact]
    public async Task GetIndexAsync_calls_getIndex_with_index()
    {
        var (runtime, module) = CreateRuntime();
        var target = new FakeJSObjectReference();
        module.ReturnValues["getIndex"] = "item";

        var result = await runtime.GetIndexAsync<string>(target, 2);

        Assert.Equal("item", result);
        var call = Assert.Single(module.Invocations, i => i.Identifier == "getIndex");
        Assert.Equal(2, call.Args![1]);
    }

    [Fact]
    public async Task SetIndexAsync_calls_setIndex_with_index_and_value()
    {
        var (runtime, module) = CreateRuntime();
        var target = new FakeJSObjectReference();

        await runtime.SetIndexAsync(target, 0, "first");

        var call = Assert.Single(module.Invocations, i => i.Identifier == "setIndex");
        Assert.Equal(0,       call.Args![1]);
        Assert.Equal("first", call.Args![2]);
    }

    [Fact]
    public async Task DomArguments_Unwrap_replaces_IDomProxy_with_Reference()
    {
        var (runtime, module) = CreateRuntime();
        var proxyRef = new FakeJSObjectReference();
        var proxyFactory = new DomProxyFactory(runtime);
        var proxy = new TestProxy(proxyRef, runtime, proxyFactory);

        var unwrapped = DomArguments.Unwrap([proxy, "literal", 42]);

        Assert.Same(proxyRef,  unwrapped![0]);
        Assert.Equal("literal", unwrapped[1]);
        Assert.Equal(42,        unwrapped[2]);
    }

    [Fact]
    public void DomArguments_Unwrap_returns_null_for_null_input()
        => Assert.Null(DomArguments.Unwrap(null));

    [Fact]
    public void DomArguments_Unwrap_returns_same_array_when_no_proxies()
    {
        object?[] args = ["a", 1];
        Assert.Same(args, DomArguments.Unwrap(args));
    }

    // ── Defect 4 regression: Server prerendering detection ────────────────

    [Fact]
    public async Task Server_throws_DomJSException_prerendering_on_prerendering_JSException()
    {
        var jsRuntime = new FakeJSRuntime();
        jsRuntime.QueueImportFailure(
            new JSException("JavaScript interop calls cannot be issued at this time."));
        var runtime = new ServerDomRuntime(jsRuntime);

        var ex = await Assert.ThrowsAsync<DomJSException>(
            () => runtime.GetGlobalAsync("window").AsTask());

        Assert.Contains("prerendering", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Server_throws_DomJSException_prerendering_on_prerendering_InvalidOperationException()
    {
        var jsRuntime = new FakeJSRuntime();
        jsRuntime.QueueImportFailure(
            new InvalidOperationException("prerendering is in progress"));
        var runtime = new ServerDomRuntime(jsRuntime);

        var ex = await Assert.ThrowsAsync<DomJSException>(
            () => runtime.GetGlobalAsync("window").AsTask());

        Assert.Contains("prerendering", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helper proxy for arg-unwrap test ──────────────────────────────────

    private sealed class TestProxy(
        IJSObjectReference reference,
        IDomRuntime runtime,
        IDomProxyFactory factory)
        : DomProxyBase(reference, runtime, factory);
}
