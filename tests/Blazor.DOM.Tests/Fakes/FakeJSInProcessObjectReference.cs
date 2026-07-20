// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.DOM.Tests.Fakes;

/// <summary>
/// Minimal <see cref="IJSInProcessObjectReference"/> fake for WASM-path testing.
/// Supports queuing exceptions on specific identifiers.
/// </summary>
public sealed class FakeJSInProcessObjectReference :
    IJSInProcessObjectReference,
    IConfigurableJSObjectReference,
    IDisposable
{
    public bool IsDisposed { get; private set; }
    public int DisposeCallCount { get; private set; }
    public List<(string Identifier, object?[]? Args)> Invocations { get; } = [];
    public Dictionary<string, object?> ReturnValues { get; } = [];
    public Dictionary<
        string,
        Func<object?[]?, CancellationToken, ValueTask<object?>>> InvocationHandlers { get; } = [];

    /// <summary>
    /// Exceptions to throw when <see cref="Invoke{TValue}"/> or
    /// <c>InvokeAsync</c> is called with the matching identifier.
    /// </summary>
    public Dictionary<string, Exception> ThrowValues { get; } = [];

    public TValue Invoke<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, params object?[]? args)
        => InvokeCoreAsync<TValue>(identifier, args, default).AsTask()
            .GetAwaiter().GetResult();

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, object?[]? args)
        => InvokeCoreAsync<TValue>(identifier, args, default);

    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeCoreAsync<TValue>(identifier, args, cancellationToken);

    private async ValueTask<TValue> InvokeCoreAsync<TValue>(
        string identifier,
        object?[]? args,
        CancellationToken cancellationToken)
    {
        Invocations.Add((identifier, args));
        if (ThrowValues.TryGetValue(identifier, out var ex))
            throw ex;
        if (InvocationHandlers.TryGetValue(identifier, out var handler))
        {
            var result = await handler(args, cancellationToken);
            return result is null ? default! : (TValue)result;
        }
        if (TryGetReferenceResult(identifier, out var resultIdentifier))
        {
            ReturnValues.TryGetValue(resultIdentifier, out var value);
            var delivery = (DotNetObjectReference<DomReferenceDeliveryHandler>)
                args![^2]!;
            var accepted = await delivery.Value.ReceiveReferenceAsync(
                value as IJSObjectReference);
            if (!accepted)
            {
                throw new JSException(".NET rejected object reference delivery.");
            }
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

    private static bool TryGetReferenceResult(
        string identifier,
        out string resultIdentifier)
    {
        resultIdentifier = identifier switch
        {
            "getPropertyDotNetObjectReference" => "getProperty",
            "invokeMethodDotNetObjectReference" => "invokeMethod",
            "getIndexDotNetObjectReference" => "getIndex",
            _ => string.Empty,
        };
        return resultIdentifier.Length > 0;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        DisposeCallCount++;
        IsDisposed = true;
    }
}
