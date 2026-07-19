// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.DOM.Tests.Fakes;

/// <summary>
/// Minimal <see cref="IJSRuntime"/> fake.  Returns the configured
/// module whenever "import" is called, unless a queued failure
/// or controllable-import mode overrides the behaviour.
/// </summary>
public sealed class FakeJSRuntime : IJSRuntime
{
    private readonly FakeJSObjectReference _fakeModule;
    private readonly IJSObjectReference _moduleRef;
    private readonly Queue<Exception> _importFailures = new();
    private TaskCompletionSource<IJSObjectReference>? _importTcs;

    public int ImportCallCount { get; private set; }

    public FakeJSRuntime(FakeJSObjectReference? module = null)
    {
        _fakeModule = module ?? new FakeJSObjectReference();
        _moduleRef  = _fakeModule;
    }

    /// <summary>
    /// Constructor overload that accepts any <see cref="IJSObjectReference"/>
    /// as the module (e.g. <see cref="SlowRemovalFakeJSObjectReference"/>).
    /// </summary>
    public FakeJSRuntime(IJSObjectReference module)
    {
        _fakeModule = new FakeJSObjectReference(); // unused; kept for Module property type
        _moduleRef  = module;
    }

    /// <summary>Returns the typed fake module when one was supplied.</summary>
    public FakeJSObjectReference Module => _fakeModule;

    /// <summary>Queues an exception to be thrown on the next "import" call.</summary>
    public void QueueImportFailure(Exception ex) => _importFailures.Enqueue(ex);

    /// <summary>
    /// Switches to controllable-import mode: the next "import" call returns a
    /// pending <see cref="Task"/> resolved only by <see cref="CompleteImport"/>
    /// or <see cref="FailImport"/>.
    /// </summary>
    public void UseControllableImport()
        => _importTcs = new TaskCompletionSource<IJSObjectReference>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes the pending controllable import successfully.</summary>
    public void CompleteImport() => _importTcs?.TrySetResult(_moduleRef);

    /// <summary>Completes the pending controllable import with a failure.</summary>
    public void FailImport(Exception ex) => _importTcs?.TrySetException(ex);

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

            return ValueTask.FromResult((TValue)(object)_moduleRef);
        }
        return ValueTask.FromResult<TValue>(default!);
    }

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);
}
