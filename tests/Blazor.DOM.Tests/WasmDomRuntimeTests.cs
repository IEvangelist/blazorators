// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.DOM.Tests.Fakes;

namespace Blazor.DOM.Tests;

/// <summary>
/// Tests for <see cref="WasmDomRuntime"/> sync and async dispatch paths.
/// </summary>
public sealed class WasmDomRuntimeTests
{
    private static (WasmDomRuntime Runtime, FakeJSInProcessObjectReference Module) CreateRuntime()
    {
        var module    = new FakeJSInProcessObjectReference();
        var jsRuntime = new FakeJSInProcessRuntime(module);
        var runtime   = new WasmDomRuntime(jsRuntime);
        return (runtime, module);
    }

    private static async Task<(WasmDomRuntime Runtime, FakeJSInProcessObjectReference Module)>
        CreateInitializedRuntime()
    {
        var (runtime, module) = CreateRuntime();
        await runtime.InitializeAsync();
        return (runtime, module);
    }

    // ── Sync paths ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProperty_sync_calls_getProperty_on_module()
    {
        var (runtime, module) = await CreateInitializedRuntime();
        var target = new FakeJSInProcessObjectReference();
        module.ReturnValues["getProperty"] = "syncValue";

        var result = runtime.GetProperty<string>(target, "title");

        Assert.Equal("syncValue", result);
        var call = Assert.Single(module.Invocations, i => i.Identifier == "getProperty");
        Assert.Equal(target,  call.Args![0]);
        Assert.Equal("title", call.Args![1]);
    }

    [Fact]
    public async Task Reference_property_and_index_sync_paths_return_in_process_references()
    {
        var (runtime, module) = await CreateInitializedRuntime();
        var target = new FakeJSInProcessObjectReference();
        var propertyReference = new FakeJSInProcessObjectReference();
        var indexReference = new FakeJSInProcessObjectReference();
        module.ReturnValues["getProperty"] = propertyReference;
        module.ReturnValues["getIndex"] = indexReference;

        var property = runtime.GetPropertyRef(target, "ownerDocument");
        var index = runtime.GetIndexRef(target, 0);

        Assert.Same(propertyReference, property);
        Assert.Same(indexReference, index);
    }

    [Fact]
    public async Task SetProperty_sync_calls_setProperty_on_module()
    {
        var (runtime, module) = await CreateInitializedRuntime();
        var target = new FakeJSInProcessObjectReference();

        runtime.SetProperty(target, "disabled", true);

        var call = Assert.Single(module.Invocations, i => i.Identifier == "setProperty");
        Assert.Equal("disabled", call.Args![1]);
        Assert.Equal(true,       call.Args![2]);
    }

    [Fact]
    public async Task InvokeMethod_sync_calls_invokeMethod_on_module()
    {
        var (runtime, module) = await CreateInitializedRuntime();
        var target = new FakeJSInProcessObjectReference();
        module.ReturnValues["invokeMethod"] = 7;

        var result = runtime.InvokeMethod<int>(target, "countChildren", null);

        Assert.Equal(7, result);
    }

    [Fact]
    public async Task InvokeMethodVoid_sync_calls_module()
    {
        var (runtime, module) = await CreateInitializedRuntime();
        var target = new FakeJSInProcessObjectReference();

        runtime.InvokeMethodVoid(target, "focus", null);

        Assert.Contains(module.Invocations, i => i.Identifier == "invokeMethod");
    }

    [Fact]
    public async Task InvokeMethodRef_sync_returns_in_process_reference()
    {
        var (runtime, module) = await CreateInitializedRuntime();
        var target   = new FakeJSInProcessObjectReference();
        var childRef = new FakeJSInProcessObjectReference();
        module.ReturnValues["invokeMethod"] = childRef;

        var result = runtime.InvokeMethodRef(target, "firstElementChild", null);

        Assert.Same(childRef, result);
    }

    [Fact]
    public async Task GetIndex_sync_calls_getIndex_on_module()
    {
        var (runtime, module) = await CreateInitializedRuntime();
        var target = new FakeJSInProcessObjectReference();
        module.ReturnValues["getIndex"] = "elem";

        var result = runtime.GetIndex<string>(target, 0);

        Assert.Equal("elem", result);
    }

    [Fact]
    public async Task SetIndex_sync_calls_setIndex_on_module()
    {
        var (runtime, module) = await CreateInitializedRuntime();
        var target = new FakeJSInProcessObjectReference();

        runtime.SetIndex(target, 1, "val");

        var call = Assert.Single(module.Invocations, i => i.Identifier == "setIndex");
        Assert.Equal(1,     call.Args![1]);
        Assert.Equal("val", call.Args![2]);
    }

    // ── Async paths ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetPropertyAsync_async_path_works_after_init()
    {
        var (runtime, module) = await CreateInitializedRuntime();
        var target = new FakeJSInProcessObjectReference();
        module.ReturnValues["getProperty"] = "asyncVal";

        var result = await runtime.GetPropertyAsync<string>(target, "href");

        Assert.Equal("asyncVal", result);
    }

    // ── Init guard ────────────────────────────────────────────────────────

    [Fact]
    public void Sync_call_without_init_throws_InvalidOperationException()
    {
        var (runtime, _) = CreateRuntime();
        var fakeRef = new FakeJSInProcessObjectReference();

        Assert.Throws<InvalidOperationException>(
            () => runtime.GetProperty<string>(fakeRef, "x"));
    }

    // ── Disposal ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_releases_module()
    {
        var (runtime, module) = await CreateInitializedRuntime();

        await runtime.DisposeAsync();

        Assert.True(module.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_is_idempotent()
    {
        var (runtime, module) = await CreateInitializedRuntime();

        await runtime.DisposeAsync();
        await runtime.DisposeAsync();

        Assert.True(module.IsDisposed);
    }

    // ── Defect 4 regression: WASM prerender parity ────────────────────────

    [Fact]
    public async Task WASM_throws_DomJSException_prerendering_on_prerendering_JSException()
    {
        // Arrange: import fails with the standard Blazor pre-rendering message.
        var jsRuntime = new FakeJSInProcessRuntime();
        jsRuntime.QueueImportFailure(
            new JSException("JavaScript interop calls cannot be issued at this time."));
        var runtime = new WasmDomRuntime(jsRuntime);

        var ex = await Assert.ThrowsAsync<DomJSException>(
            () => runtime.GetGlobalAsync("window").AsTask());

        Assert.Contains("prerendering", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WASM_throws_DomJSException_prerendering_on_prerendering_InvalidOperationException()
    {
        var jsRuntime = new FakeJSInProcessRuntime();
        jsRuntime.QueueImportFailure(
            new InvalidOperationException("prerendering is in progress"));
        var runtime = new WasmDomRuntime(jsRuntime);

        var ex = await Assert.ThrowsAsync<DomJSException>(
            () => runtime.GetGlobalAsync("window").AsTask());

        Assert.Contains("prerendering", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WASM_prerendering_failure_is_cleared_so_retry_succeeds()
    {
        // Pre-rendering is transient: after the circuit activates the next call must work.
        var jsRuntime = new FakeJSInProcessRuntime();
        jsRuntime.QueueImportFailure(
            new JSException("JavaScript interop calls cannot be issued at this time."));
        var runtime = new WasmDomRuntime(jsRuntime);

        await Assert.ThrowsAsync<DomJSException>(
            () => runtime.GetGlobalAsync("window").AsTask());

        // Second call: import retried, succeeds.
        await runtime.GetGlobalAsync("document");

        Assert.Equal(2, jsRuntime.ImportCallCount);
    }
}
