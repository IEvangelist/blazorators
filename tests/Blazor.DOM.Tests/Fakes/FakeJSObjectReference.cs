// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.DOM.Tests.Fakes;

/// <summary>
/// Minimal <see cref="IJSObjectReference"/> fake that records invocations and
/// returns configurable values.  Supports queuing exceptions to be thrown on
/// specific identifier calls, and controlled (non-synchronous) import tasks.
/// </summary>
public sealed class FakeJSObjectReference : IConfigurableJSObjectReference
{
    public bool IsDisposed { get; private set; }
    public int DisposeCallCount { get; private set; }
    public Func<ValueTask>? DisposeHandler { get; init; }
    public List<(string Identifier, object?[]? Args)> Invocations { get; } = [];
    public Dictionary<string, object?> ReturnValues { get; } = [];
    public Dictionary<
        string,
        Func<object?[]?, CancellationToken, ValueTask<object?>>> InvocationHandlers { get; } = [];

    /// <summary>
    /// Exceptions to throw when <c>InvokeAsync</c> is called
    /// with the matching identifier.  Set a value to make the invocation fail.
    /// </summary>
    public Dictionary<string, Exception> ThrowValues { get; } = [];

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

    public async ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        if (DisposeHandler is not null)
        {
            await DisposeHandler().ConfigureAwait(false);
        }
        IsDisposed = true;
    }
}
