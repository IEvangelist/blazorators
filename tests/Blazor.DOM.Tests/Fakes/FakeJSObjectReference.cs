// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.DOM.Tests.Fakes;

/// <summary>
/// Minimal <see cref="IJSObjectReference"/> fake that records invocations and
/// returns configurable values.  Supports queuing exceptions to be thrown on
/// specific identifier calls, and controlled (non-synchronous) import tasks.
/// </summary>
public sealed class FakeJSObjectReference : IJSObjectReference
{
    public bool IsDisposed { get; private set; }
    public List<(string Identifier, object?[]? Args)> Invocations { get; } = [];
    public Dictionary<string, object?> ReturnValues { get; } = [];

    /// <summary>
    /// Exceptions to throw when <c>InvokeAsync</c> is called
    /// with the matching identifier.  Set a value to make the invocation fail.
    /// </summary>
    public Dictionary<string, Exception> ThrowValues { get; } = [];

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, object?[]? args)
    {
        Invocations.Add((identifier, args));
        if (ThrowValues.TryGetValue(identifier, out var ex))
            return ValueTask.FromException<TValue>(ex);
        if (ReturnValues.TryGetValue(identifier, out var val))
            return ValueTask.FromResult((TValue)val!);
        return ValueTask.FromResult<TValue>(default!);
    }

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
