// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.DOM.Tests.Fakes;

namespace Blazor.DOM.Tests;

/// <summary>
/// Tests for ownership semantics and idempotent disposal of
/// <see cref="DomObjectReference"/>, <see cref="DomProxyBase"/>, and
/// <see cref="WasmDomProxyBase"/>.
/// </summary>
public sealed class DisposalTests
{
    // ── DomObjectReference ────────────────────────────────────────────────

    [Fact]
    public async Task Owned_reference_disposes_underlying_ref()
    {
        var jsRef   = new FakeJSObjectReference();
        var domRef  = DomObjectReference.Owned(jsRef);

        await domRef.DisposeAsync();

        Assert.True(jsRef.IsDisposed);
    }

    [Fact]
    public async Task Shared_reference_does_not_dispose_underlying_ref()
    {
        var jsRef   = new FakeJSObjectReference();
        var domRef  = DomObjectReference.Shared(jsRef);

        await domRef.DisposeAsync();

        Assert.False(jsRef.IsDisposed);
    }

    [Fact]
    public async Task Owned_reference_disposal_is_idempotent()
    {
        var jsRef  = new FakeJSObjectReference();
        var domRef = DomObjectReference.Owned(jsRef);

        await domRef.DisposeAsync();
        await domRef.DisposeAsync(); // must not call DisposeAsync on jsRef again

        Assert.True(jsRef.IsDisposed);
    }

    // ── DomProxyBase ──────────────────────────────────────────────────────

    [Fact]
    public async Task DomProxyBase_disposes_underlying_reference()
    {
        var jsRef   = new FakeJSObjectReference();
        var runtime = new ServerDomRuntime(new FakeJSRuntime());
        var factory = new DomProxyFactory(runtime);
        var proxy   = new TestProxy(jsRef, runtime, factory);

        await proxy.DisposeAsync();

        Assert.True(jsRef.IsDisposed);
    }

    [Fact]
    public async Task DomProxyBase_disposal_is_idempotent()
    {
        var jsRef   = new FakeJSObjectReference();
        var runtime = new ServerDomRuntime(new FakeJSRuntime());
        var factory = new DomProxyFactory(runtime);
        var proxy   = new TestProxy(jsRef, runtime, factory);

        await proxy.DisposeAsync();
        await proxy.DisposeAsync();

        Assert.True(jsRef.IsDisposed);
    }

    // ── WasmDomProxyBase ──────────────────────────────────────────────────

    [Fact]
    public void WasmDomProxyBase_InProcessReference_throws_when_not_in_process()
    {
        var jsRef    = new FakeJSObjectReference(); // NOT in-process
        var runtime  = new WasmDomRuntime(new FakeJSInProcessRuntime());
        var factory  = new DomProxyFactory(runtime);
        var proxy    = new TestWasmProxy(jsRef, runtime, factory);

        Assert.Throws<InvalidOperationException>(() => proxy.GetInProcessRef());
    }

    [Fact]
    public void WasmDomProxyBase_SyncRuntime_throws_when_runtime_not_sync()
    {
        var jsRef    = new FakeJSObjectReference();
        var runtime  = new ServerDomRuntime(new FakeJSRuntime()); // not IDomSyncRuntime
        var factory  = new DomProxyFactory(runtime);
        var proxy    = new TestWasmProxy(jsRef, runtime, factory);

        Assert.Throws<InvalidOperationException>(() => proxy.GetSyncRuntime());
    }

    [Fact]
    public void WasmDomProxyBase_SyncRuntime_returns_sync_runtime_on_wasm()
    {
        var jsRef    = new FakeJSInProcessObjectReference();
        var jsRt     = new FakeJSInProcessRuntime();
        var runtime  = new WasmDomRuntime(jsRt);
        var factory  = new DomProxyFactory(runtime);
        var proxy    = new TestWasmProxy(jsRef, runtime, factory);

        var syncRt = proxy.GetSyncRuntime();

        Assert.Same(runtime, syncRt);
    }

    // ── Helper proxies ────────────────────────────────────────────────────

    private sealed class TestProxy(
        IJSObjectReference reference,
        IDomRuntime runtime,
        IDomProxyFactory factory) : DomProxyBase(reference, runtime, factory);

    private sealed class TestWasmProxy(
        IJSObjectReference reference,
        IDomRuntime runtime,
        IDomProxyFactory factory) : WasmDomProxyBase(reference, runtime, factory)
    {
        public IDomSyncRuntime     GetSyncRuntime()    => SyncRuntime;
        public IJSInProcessObjectReference GetInProcessRef() => InProcessReference;
    }
}
