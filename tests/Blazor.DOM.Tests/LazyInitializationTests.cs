// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.DOM.Tests.Fakes;

namespace Blazor.DOM.Tests;

/// <summary>
/// Verifies that the module is imported lazily — only on first use — and
/// that repeated calls reuse the cached import.
/// </summary>
public sealed class LazyInitializationTests
{
    [Fact]
    public void Module_paths_follow_the_assembly_containing_each_runtime()
    {
        Assert.Equal(
            DomModulePath.ForAssemblyContaining<ServerDomRuntime>(),
            ServerDomRuntime.ModulePath);
        Assert.Equal(
            DomModulePath.ForAssemblyContaining<WasmDomRuntime>(),
            WasmDomRuntime.ModulePath);
        Assert.NotEqual(ServerDomRuntime.ModulePath, WasmDomRuntime.ModulePath);
    }

    [Fact]
    public async Task Module_is_not_imported_until_first_operation()
    {
        var jsRuntime = new FakeJSRuntime();
        var runtime   = new ServerDomRuntime(jsRuntime);

        Assert.Equal(0, jsRuntime.ImportCallCount);

        await runtime.GetGlobalAsync("window");

        Assert.Equal(1, jsRuntime.ImportCallCount);
    }

    [Fact]
    public async Task Module_is_imported_exactly_once_across_multiple_calls()
    {
        var jsRuntime = new FakeJSRuntime();
        var runtime   = new ServerDomRuntime(jsRuntime);
        var target    = new FakeJSObjectReference();

        await runtime.GetGlobalAsync("window");
        await runtime.GetPropertyAsync<string>(target, "title");
        await runtime.GetPropertyAsync<string>(target, "body");

        Assert.Equal(1, jsRuntime.ImportCallCount);
    }

    [Fact]
    public async Task Disposal_before_first_use_does_not_throw()
    {
        var runtime = new ServerDomRuntime(new FakeJSRuntime());
        // Dispose without ever triggering the lazy module — should be a no-op.
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task Disposal_after_use_releases_module()
    {
        var fakeModule = new FakeJSObjectReference();
        var jsRuntime  = new FakeJSRuntime(fakeModule);
        var runtime    = new ServerDomRuntime(jsRuntime);

        await runtime.GetGlobalAsync("window");

        Assert.False(fakeModule.IsDisposed);

        await runtime.DisposeAsync();

        Assert.True(fakeModule.IsDisposed);
    }

    [Fact]
    public async Task Disposal_is_idempotent()
    {
        var fakeModule = new FakeJSObjectReference();
        var jsRuntime  = new FakeJSRuntime(fakeModule);
        var runtime    = new ServerDomRuntime(jsRuntime);

        await runtime.GetGlobalAsync("window");
        await runtime.DisposeAsync();
        await runtime.DisposeAsync(); // second dispose must not re-dispose the module

        // The module should have been disposed exactly once — if the fake
        // tracked dispose counts we'd assert 1; as-is we just assert no throw.
        Assert.True(fakeModule.IsDisposed);
    }

    [Fact]
    public async Task WASM_module_is_not_imported_until_first_async_operation()
    {
        var jsRuntime  = new FakeJSInProcessRuntime();
        var wasmRuntime = new WasmDomRuntime(jsRuntime);

        // No import should have happened yet.
        Assert.Equal(0, jsRuntime.ImportCallCount);

        await wasmRuntime.GetGlobalAsync("window");

        Assert.Equal(1, jsRuntime.ImportCallCount);
    }

    [Fact]
    public void WASM_sync_call_before_init_throws_informative_exception()
    {
        var jsRuntime   = new FakeJSInProcessRuntime();
        var wasmRuntime = new WasmDomRuntime(jsRuntime);
        var fakeRef     = new FakeJSInProcessObjectReference();

        var ex = Assert.Throws<InvalidOperationException>(
            () => wasmRuntime.GetProperty<string>(fakeRef, "title"));

        Assert.Contains("not been initialised", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Defect 3 regression: cached failed import ─────────────────────────

    [Fact]
    public async Task Server_failed_import_is_cleared_and_retried_on_next_call()
    {
        var jsRuntime = new FakeJSRuntime();
        jsRuntime.QueueImportFailure(new JSException("transient error"));
        var runtime = new ServerDomRuntime(jsRuntime);

        // First call: import fails.
        await Assert.ThrowsAsync<JSException>(
            () => runtime.GetGlobalAsync("window").AsTask());

        Assert.Equal(1, jsRuntime.ImportCallCount);

        // Second call: import should be retried (not permanently cached as failed).
        await runtime.GetGlobalAsync("document");

        Assert.Equal(2, jsRuntime.ImportCallCount);
    }

    [Fact]
    public async Task Wasm_failed_import_is_cleared_and_retried_on_next_call()
    {
        var jsRuntime = new FakeJSInProcessRuntime();
        jsRuntime.QueueImportFailure(new JSException("transient error"));
        var runtime = new WasmDomRuntime(jsRuntime);

        await Assert.ThrowsAsync<JSException>(
            () => runtime.GetGlobalAsync("window").AsTask());

        Assert.Equal(1, jsRuntime.ImportCallCount);

        await runtime.GetGlobalAsync("document");

        Assert.Equal(2, jsRuntime.ImportCallCount);
    }

    [Fact]
    public async Task Server_cancelled_caller_does_not_poison_shared_import_task()
    {
        // Arrange: use controllable import so we can separate caller cancellation
        // from the shared import completing.
        var jsRuntime = new FakeJSRuntime();
        jsRuntime.UseControllableImport();
        var runtime = new ServerDomRuntime(jsRuntime);

        // Caller A: starts import, then cancels.
        var cts = new CancellationTokenSource();
        var callerATask = runtime.GetGlobalAsync("window", cts.Token).AsTask();

        // Cancel caller A while the import is still in progress.
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callerATask);

        // Complete the import AFTER caller A cancelled.
        jsRuntime.CompleteImport();

        // Caller B: must not throw and must not re-import — the shared task
        // completed successfully after caller A was cancelled.
        var callerBTask = runtime.GetGlobalAsync("window"); // result from fake is null; that's fine
        await callerBTask; // must not throw

        // Import started only once — caller cancellation didn't poison the task.
        Assert.Equal(1, jsRuntime.ImportCallCount);
    }

    [Fact]
    public async Task Server_concurrent_GetModuleAsync_results_in_single_import()
    {
        // Arrange: slow import so both callers are in-flight simultaneously.
        var jsRuntime = new FakeJSRuntime();
        jsRuntime.UseControllableImport();
        var runtime = new ServerDomRuntime(jsRuntime);

        // Start two concurrent module-requiring calls.
        var task1 = runtime.GetGlobalAsync("window").AsTask();
        var task2 = runtime.GetGlobalAsync("document").AsTask();

        // Allow them to progress to the import await, then complete the import.
        await Task.Yield();
        jsRuntime.CompleteImport();

        await Task.WhenAll(task1, task2);

        // Only one JS "import" call must have been issued.
        Assert.Equal(1, jsRuntime.ImportCallCount);
    }

    [Fact]
    public async Task Wasm_concurrent_GetModuleAsync_results_in_single_import()
    {
        var jsRuntime = new FakeJSInProcessRuntime();
        jsRuntime.UseControllableImport();
        var runtime = new WasmDomRuntime(jsRuntime);

        var task1 = runtime.GetGlobalAsync("window").AsTask();
        var task2 = runtime.GetGlobalAsync("document").AsTask();

        await Task.Yield();
        jsRuntime.CompleteImport();

        await Task.WhenAll(task1, task2);

        Assert.Equal(1, jsRuntime.ImportCallCount);
    }

    // ── Defect 3 & 4 regression: dispose-after-failure / TOCTOU ──────────

    [Fact]
    public async Task Server_dispose_after_failed_import_does_not_throw()
    {
        var jsRuntime = new FakeJSRuntime();
        jsRuntime.QueueImportFailure(new JSException("import failed"));
        var runtime = new ServerDomRuntime(jsRuntime);

        // Trigger the failing import.
        await Assert.ThrowsAsync<JSException>(() => runtime.GetGlobalAsync("window").AsTask());

        // Disposal with no successfully imported module must be a no-op.
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task Wasm_dispose_after_failed_import_does_not_throw()
    {
        var jsRuntime = new FakeJSInProcessRuntime();
        jsRuntime.QueueImportFailure(new JSException("import failed"));
        var runtime = new WasmDomRuntime(jsRuntime);

        await Assert.ThrowsAsync<JSException>(() => runtime.GetGlobalAsync("window").AsTask());

        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task Server_GetModuleAsync_after_DisposeAsync_throws_ObjectDisposedException()
    {
        var jsRuntime = new FakeJSRuntime();
        var runtime = new ServerDomRuntime(jsRuntime);

        await runtime.GetGlobalAsync("window"); // trigger import
        await runtime.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => runtime.GetGlobalAsync("document").AsTask());
    }

    [Fact]
    public async Task Wasm_GetModuleAsync_after_DisposeAsync_throws_ObjectDisposedException()
    {
        var jsRuntime = new FakeJSInProcessRuntime();
        var runtime = new WasmDomRuntime(jsRuntime);

        await runtime.GetGlobalAsync("window");
        await runtime.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => runtime.GetGlobalAsync("document").AsTask());
    }

    /// <summary>
    /// Deterministic TOCTOU regression: <see cref="ServerDomRuntime.DisposeAsync"/>
    /// races with an in-flight <c>GetModuleAsync</c>.  The module imported while
    /// disposal was in progress must be disposed and must not leak.
    /// </summary>
    [Fact]
    public async Task Server_dispose_races_concurrent_import_module_is_not_leaked()
    {
        var jsRuntime = new FakeJSRuntime();
        jsRuntime.UseControllableImport();
        var runtime = new ServerDomRuntime(jsRuntime);
        var fakeModule = jsRuntime.Module;

        // GetGlobalAsync holds _importLock while awaiting the import.
        var importTask = runtime.GetGlobalAsync("window").AsTask();
        await Task.Yield(); // let importTask reach the lock and start the TCS-backed import

        // DisposeAsync sets _disposed=1 then blocks on _importLock (behind importTask).
        var disposeTask = runtime.DisposeAsync().AsTask();
        await Task.Yield(); // let disposeTask reach the WaitAsync for the lock

        // Completing the import wakes up importTask inside the lock.  It detects
        // _disposed=1, disposes the module inline, then throws ObjectDisposedException.
        jsRuntime.CompleteImport();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => importTask);
        await disposeTask; // must complete cleanly (module already disposed by importTask)

        // No successfully imported module may escape without being disposed.
        Assert.True(fakeModule.IsDisposed);
    }

    /// <summary>
    /// Deterministic TOCTOU regression for WASM: <see cref="WasmDomRuntime.DisposeAsync"/>
    /// races with an in-flight <c>GetAsyncModuleAsync</c>.  The module imported while
    /// disposal was in progress must be disposed and must not leak.
    /// </summary>
    [Fact]
    public async Task Wasm_dispose_races_concurrent_import_module_is_not_leaked()
    {
        var jsRuntime = new FakeJSInProcessRuntime();
        jsRuntime.UseControllableImport();
        var runtime = new WasmDomRuntime(jsRuntime);
        var fakeModule = jsRuntime.Module;

        // GetGlobalAsync is suspended awaiting the import.
        var importTask = runtime.GetGlobalAsync("window").AsTask();
        await Task.Yield();

        // DisposeAsync sets _disposed=1 then tries to acquire _importLock (behind importTask).
        var disposeTask = runtime.DisposeAsync().AsTask();
        await Task.Yield(); // let disposeTask reach the WaitAsync for the lock

        // Complete the import — GetAsyncModuleAsync resumes inside the lock, detects
        // _disposed=1, disposes the module inline, then throws ObjectDisposedException.
        jsRuntime.CompleteImport();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => importTask);
        await disposeTask; // DisposeAsync captures null _importTask (already cleared) → clean

        Assert.True(fakeModule.IsDisposed);
    }

    /// <summary>
    /// Regression: if a caller's <c>WaitAsync(ct)</c> token is cancelled while the
    /// underlying import <see cref="Task"/> is still running,
    /// <see cref="ServerDomRuntime.DisposeAsync"/> must await the in-flight task and
    /// dispose the module when it succeeds — not silently leak it.
    /// </summary>
    [Fact]
    public async Task Server_DisposeAsync_awaits_in_flight_import_and_disposes_module()
    {
        var jsRuntime  = new FakeJSRuntime();
        jsRuntime.UseControllableImport();
        var runtime    = new ServerDomRuntime(jsRuntime);
        var fakeModule = jsRuntime.Module;

        // Caller A starts the import then has its token cancelled.  The underlying
        // TCS-backed import task keeps running; _importTask is still set.
        using var cts = new CancellationTokenSource();
        var callerA = runtime.GetGlobalAsync("window", cts.Token).AsTask();
        await Task.Yield();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callerA);

        // DisposeAsync must capture and await the still-running import task.
        var disposeTask = runtime.DisposeAsync().AsTask();
        await Task.Yield();

        // Resolving the import hands the module to DisposeAsync, which must dispose it.
        jsRuntime.CompleteImport();
        await disposeTask;

        Assert.True(fakeModule.IsDisposed);
    }

    /// <summary>
    /// Regression: same in-flight leak scenario for <see cref="WasmDomRuntime.DisposeAsync"/>.
    /// </summary>
    [Fact]
    public async Task Wasm_DisposeAsync_awaits_in_flight_import_and_disposes_module()
    {
        var jsRuntime  = new FakeJSInProcessRuntime();
        jsRuntime.UseControllableImport();
        var runtime    = new WasmDomRuntime(jsRuntime);
        var fakeModule = jsRuntime.Module;

        using var cts = new CancellationTokenSource();
        var callerA = runtime.GetGlobalAsync("window", cts.Token).AsTask();
        await Task.Yield();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callerA);

        var disposeTask = runtime.DisposeAsync().AsTask();
        await Task.Yield();

        jsRuntime.CompleteImport();
        await disposeTask;

        Assert.True(fakeModule.IsDisposed);
    }
}
