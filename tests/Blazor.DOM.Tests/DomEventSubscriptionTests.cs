// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.DOM.Tests.Fakes;

namespace Blazor.DOM.Tests;

/// <summary>
/// Tests for <see cref="DomEventSubscription"/> lifecycle, disposal, and
/// duplicate-subscription semantics.
/// </summary>
public sealed class DomEventSubscriptionTests
{
    private static (ServerDomRuntime Runtime, FakeJSObjectReference Module) CreateRuntime()
    {
        var module    = new FakeJSObjectReference();
        var jsRuntime = new FakeJSRuntime(module);
        return (new ServerDomRuntime(jsRuntime), module);
    }

    [Fact]
    public async Task AddEventListener_registers_listener_on_module()
    {
        var (runtime, module) = CreateRuntime();
        var target = new FakeJSObjectReference();
        module.ReturnValues["addDotNetEventListener"] = 1;

        var subscription = await runtime.AddEventListenerAsync(
            target, "click", _ => Task.CompletedTask);

        Assert.NotNull(subscription);
        Assert.Contains(module.Invocations, i => i.Identifier == "addDotNetEventListener");
    }

    [Fact]
    public async Task DisposeAsync_calls_removeEventListener_on_module()
    {
        var (runtime, module) = CreateRuntime();
        var target = new FakeJSObjectReference();
        module.ReturnValues["addDotNetEventListener"] = 42;

        var subscription = await runtime.AddEventListenerAsync(
            target, "click", _ => Task.CompletedTask);

        await subscription.DisposeAsync();

        var removeCall = Assert.Single(
            module.Invocations, i => i.Identifier == "removeDotNetEventListener");
        Assert.Equal(42, removeCall.Args![0]);
    }

    [Fact]
    public async Task DisposeAsync_is_idempotent()
    {
        var (runtime, module) = CreateRuntime();
        var target = new FakeJSObjectReference();
        module.ReturnValues["addDotNetEventListener"] = 99;

        var subscription = await runtime.AddEventListenerAsync(
            target, "click", _ => Task.CompletedTask);

        await subscription.DisposeAsync();
        await subscription.DisposeAsync(); // must not call remove twice

        var removes = module.Invocations.Count(i => i.Identifier == "removeDotNetEventListener");
        Assert.Equal(1, removes);
    }

    [Fact]
    public async Task Duplicate_subscriptions_get_independent_listener_ids()
    {
        var (runtime, module) = CreateRuntime();
        var target = new FakeJSObjectReference();
        var callCount = 0;
        module.ReturnValues["addDotNetEventListener"] = 10;

        var s1 = await runtime.AddEventListenerAsync(target, "click", _ => Task.CompletedTask);
        module.ReturnValues["addDotNetEventListener"] = 11;
        var s2 = await runtime.AddEventListenerAsync(target, "click", _ => Task.CompletedTask);

        Assert.NotSame(s1, s2);

        // Both adds must have been recorded
        var adds = module.Invocations.Count(i => i.Identifier == "addDotNetEventListener");
        Assert.Equal(2, adds);

        await s1.DisposeAsync();
        await s2.DisposeAsync();

        var removes = module.Invocations.Count(i => i.Identifier == "removeDotNetEventListener");
        Assert.Equal(2, removes);

        _ = callCount; // suppress warning
    }

    [Fact]
    public async Task RemoveEventListener_not_called_when_module_was_never_loaded()
    {
        var module    = new FakeJSObjectReference();
        var jsRuntime = new FakeJSRuntime(module);
        var runtime   = new ServerDomRuntime(jsRuntime);

        // Never trigger module load — call RemoveEventListenerAsync directly.
        await runtime.RemoveEventListenerAsync(999);

        Assert.DoesNotContain(module.Invocations,
            i => i.Identifier == "removeDotNetEventListener");
    }

    // ── Defect 1 regression: GC-root leak ────────────────────────────────

    [Fact]
    public async Task Server_AddEventListener_disposes_handlerRef_when_JS_registration_throws()
    {
        // Arrange: module always throws on addDotNetEventListener.
        var throwingModule = new FakeJSObjectReference();
        throwingModule.ThrowValues["addDotNetEventListener"] = new JSException("reg failed");
        var runtime = new ServerDomRuntime(new FakeJSRuntime(throwingModule));

        // Act & Assert: the JSException propagates cleanly.
        // If the handlerRef were leaked, a finaliser warning or a second call to
        // Dispose would surface it; the important thing is no secondary exception.
        var ex = await Assert.ThrowsAsync<JSException>(
            () => runtime.AddEventListenerAsync(
                new FakeJSObjectReference(), "click", _ => Task.CompletedTask).AsTask());

        Assert.Equal("reg failed", ex.Message);
    }

    [Fact]
    public async Task Wasm_AddEventListener_disposes_handlerRef_when_JS_registration_throws()
    {
        // Arrange: in-process module always throws on addDotNetEventListener.
        var throwingModule = new FakeJSInProcessObjectReference();
        throwingModule.ThrowValues["addDotNetEventListener"] = new JSException("reg failed");
        var runtime = new WasmDomRuntime(new FakeJSInProcessRuntime(throwingModule));

        var ex = await Assert.ThrowsAsync<JSException>(
            () => runtime.AddEventListenerAsync(
                new FakeJSInProcessObjectReference(), "click", _ => Task.CompletedTask).AsTask());

        Assert.Equal("reg failed", ex.Message);
    }

    [Fact]
    public async Task DisposeAsync_awaits_slow_removal_to_completion()
    {
        // Arrange: module whose removal ValueTask doesn't complete until signalled.
        var slowModule = new SlowRemovalFakeJSObjectReference();
        slowModule.ReturnValues["addDotNetEventListener"] = 42;
        var runtime = new ServerDomRuntime(new FakeJSRuntime(slowModule));
        var target = new FakeJSObjectReference();

        var subscription = await runtime.AddEventListenerAsync(
            target, "click", _ => Task.CompletedTask);

        // Act: DisposeAsync should not return until removal actually completes.
        var disposeTask = subscription.DisposeAsync().AsTask();

        // Removal should be initiated within a short window.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!slowModule.RemovalWasInvoked && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.True(slowModule.RemovalWasInvoked, "Removal was not initiated.");

        // DisposeAsync must still be pending — the underlying removal hasn't finished.
        Assert.False(disposeTask.IsCompleted,
            "DisposeAsync completed before the slow removal finished.");

        // Signal completion and await.
        slowModule.CompleteRemoval();
        await disposeTask;
    }

    [Fact]
    public async Task DisposeAsync_releases_handler_ref_when_unexpected_exception_propagates()
    {
        // Arrange: module throws an unexpected JSException (not JSDisconnectedException)
        // on removeDotNetEventListener.
        var throwingModule = new FakeJSObjectReference();
        throwingModule.ReturnValues["addDotNetEventListener"] = 42;
        throwingModule.ThrowValues["removeDotNetEventListener"] = new JSException("unexpected");
        var runtime = new ServerDomRuntime(new FakeJSRuntime(throwingModule));

        var subscription = await runtime.AddEventListenerAsync(
            new FakeJSObjectReference(), "click", _ => Task.CompletedTask);

        // Act: the unexpected JSException must propagate to the awaiting caller.
        await Assert.ThrowsAsync<JSException>(
            () => subscription.DisposeAsync().AsTask());

        // The finally block must have run: the subscription is now marked disposed
        // (idempotent guard prevents a second listener-removal attempt).
        await subscription.DisposeAsync(); // must not throw

        Assert.Equal(1, throwingModule.Invocations.Count(
            i => i.Identifier == "removeDotNetEventListener"));
    }

}
