// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.DOM.Tests.Fakes;

/// <summary>
/// Minimal <see cref="IJSInProcessObjectReference"/> fake for WASM-path testing.
/// Supports queuing exceptions on specific identifiers.
/// </summary>
public sealed class FakeJSInProcessObjectReference : IJSInProcessObjectReference, IDisposable
{
    public bool IsDisposed { get; private set; }
    public List<(string Identifier, object?[]? Args)> Invocations { get; } = [];
    public Dictionary<string, object?> ReturnValues { get; } = [];

    /// <summary>
    /// Exceptions to throw when <see cref="Invoke{TValue}"/> or
    /// <c>InvokeAsync</c> is called with the matching identifier.
    /// </summary>
    public Dictionary<string, Exception> ThrowValues { get; } = [];

    public TValue Invoke<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, params object?[]? args)
    {
        Invocations.Add((identifier, args));
        if (ThrowValues.TryGetValue(identifier, out var ex))
            throw ex;
        if (ReturnValues.TryGetValue(identifier, out var val))
            return (TValue)val!;
        return default!;
    }

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

    public void Dispose() => IsDisposed = true;
}
