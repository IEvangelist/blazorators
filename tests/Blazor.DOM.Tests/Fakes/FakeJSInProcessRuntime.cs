// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.DOM.Tests.Fakes;

/// <summary>
/// Minimal <see cref="IJSInProcessRuntime"/> fake for WASM tests.
/// Returns a <see cref="FakeJSInProcessObjectReference"/> for "import" calls.
/// Supports queued failures and controllable-import mode for regression tests.
/// </summary>
public sealed class FakeJSInProcessRuntime : IJSInProcessRuntime
{
    private readonly FakeJSInProcessObjectReference _module;
    private readonly Queue<Exception> _importFailures = new();
    private TaskCompletionSource<FakeJSInProcessObjectReference>? _importTcs;

    public int ImportCallCount { get; private set; }

    public FakeJSInProcessRuntime(FakeJSInProcessObjectReference? module = null)
        => _module = module ?? new FakeJSInProcessObjectReference();

    public FakeJSInProcessObjectReference Module => _module;

    /// <summary>Queues an exception for the next "import" call.</summary>
    public void QueueImportFailure(Exception ex) => _importFailures.Enqueue(ex);

    /// <summary>
    /// Switches to controllable-import mode.  The next "import" call returns a
    /// pending <see cref="Task"/> resolved only by <see cref="CompleteImport"/>
    /// or <see cref="FailImport"/>.
    /// </summary>
    public void UseControllableImport()
        => _importTcs = new TaskCompletionSource<FakeJSInProcessObjectReference>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes the pending controllable import successfully.</summary>
    public void CompleteImport() => _importTcs?.TrySetResult(_module);

    /// <summary>Completes the pending controllable import with a failure.</summary>
    public void FailImport(Exception ex) => _importTcs?.TrySetException(ex);

    public TValue Invoke<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, params object?[]? args)
        => InvokeAsync<TValue>(identifier, args).GetAwaiter().GetResult();

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, object?[]? args)
    {
        if (identifier == "import")
        {
            ImportCallCount++;

            if (_importTcs is { } tcs)
                return new ValueTask<TValue>(
                    tcs.Task.ContinueWith(t => (TValue)(object)t.GetAwaiter().GetResult(),
                        TaskContinuationOptions.ExecuteSynchronously));

            if (_importFailures.TryDequeue(out var ex))
                return ValueTask.FromException<TValue>(ex);

            return ValueTask.FromResult((TValue)(object)_module);
        }
        return ValueTask.FromResult<TValue>(default!);
    }

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);
}
