// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.DOM.Tests.Fakes;

/// <summary>
/// A <see cref="IJSObjectReference"/> fake whose
/// <c>removeDotNetEventListener</c> invocation does not complete until
/// <see cref="CompleteRemoval"/> is called.  Use this to verify that
/// <c>DomEventSubscription.DisposeAsync</c> does not block the calling thread
/// while awaiting an asynchronous JS round-trip — which would deadlock a
/// Blazor Server circuit's synchronisation context.
/// </summary>
public sealed class SlowRemovalFakeJSObjectReference : IJSObjectReference
{
    private readonly TaskCompletionSource _removalTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsDisposed       { get; private set; }
    public bool RemovalWasInvoked { get; private set; }

    public List<(string Identifier, object?[]? Args)> Invocations { get; } = [];
    public Dictionary<string, object?> ReturnValues { get; } = [];

    public async ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, object?[]? args)
    {
        Invocations.Add((identifier, args));
        if (identifier == "removeDotNetEventListener")
        {
            RemovalWasInvoked = true;
            // Return a ValueTask that only completes when CompleteRemoval() is called.
            await _removalTcs.Task;
            return default!;
        }
        if (ReturnValues.TryGetValue(identifier, out var val))
        {
            if (identifier == "addDotNetEventListener" && val is int listenerId)
            {
                var registration = (DotNetObjectReference<DomEventIdRegistrationHandler>)
                    args![4]!;
                await registration.Value.ReceiveRegistrationAsync(listenerId);
                return default!;
            }
            return (TValue)val!;
        }
        return default!;
    }

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }

    /// <summary>Signals the pending removal to complete successfully.</summary>
    public void CompleteRemoval() => _removalTcs.TrySetResult();

    /// <summary>Signals the pending removal to complete with cancellation.</summary>
    public void CancelRemoval() => _removalTcs.TrySetCanceled();
}
