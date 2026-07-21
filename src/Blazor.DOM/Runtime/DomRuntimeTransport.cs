// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal static class DomRuntimeTransport
{
    public static async ValueTask<int> AddEventListenerAsync(
        IJSObjectReference module,
        IJSObjectReference target,
        string type,
        DotNetObjectReference<DomCallbackHandler> handlerReference,
        DomEventListenerOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(handlerReference);

        var registrationHandler = new DomEventIdRegistrationHandler();
        var registrationHandlerReference =
            DotNetObjectReference.Create(registrationHandler);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await module.InvokeVoidAsync(
                "addDotNetEventListener",
                cancellationToken,
                [
                    target,
                    type,
                    handlerReference,
                    "HandleEvent",
                    registrationHandlerReference,
                    "ReceiveRegistration",
                    options?.ToInteropValue(),
                ]).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return registrationHandler.TakeRegistration();
        }
        catch (Exception registrationFailure)
        {
            var listenerId = registrationHandler.CloseAndTakeRegistration();
            if (listenerId is not null)
            {
                try
                {
                    await module.InvokeVoidAsync(
                        "removeDotNetEventListener",
                        [listenerId.Value]).ConfigureAwait(false);
                }
                catch (JSDisconnectedException)
                {
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(
                        "Event registration failed and listener rollback also failed.",
                        registrationFailure,
                        cleanupFailure);
                }
            }
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(registrationFailure)
                .Throw();
            throw;
        }
        finally
        {
            registrationHandlerReference.Dispose();
        }
    }

    public static ValueTask<IJSObjectReference> GetPropertyObjectReferenceAsync(
        IJSObjectReference module,
        IJSObjectReference target,
        string name,
        CancellationToken cancellationToken) =>
        ReceiveRequiredObjectReferenceAsync(
            module,
            "getPropertyDotNetObjectReference",
            [target, name],
            cancellationToken);

    public static ValueTask<IJSObjectReference> InvokeMethodObjectReferenceAsync(
        IJSObjectReference module,
        IJSObjectReference target,
        string name,
        object?[]? args,
        CancellationToken cancellationToken) =>
        ReceiveRequiredObjectReferenceAsync(
            module,
            "invokeMethodDotNetObjectReference",
            [target, name, args],
            cancellationToken);

    public static async ValueTask<TResult> InvokeMethodUnionAsync<TResult>(
        IJSObjectReference module,
        IJSObjectReference target,
        string name,
        object?[]? args,
        IReadOnlyList<DomUnionInboundArm<TResult>> arms,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(arms);
        if (arms.Count == 0)
            throw new ArgumentException("At least one union arm is required.", nameof(arms));
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var arm in arms)
        {
            var identity = $"{arm.Discriminator}:{arm.Brand}:{arm.Literal}";
            if (!identities.Add(identity))
            {
                throw new ArgumentException(
                    $"Duplicate inbound union discriminator '{identity}'.",
                    nameof(arms));
            }
        }

        var handler = new DomUnionDeliveryHandler<TResult>(arms);
        var handlerReference = DotNetObjectReference.Create(handler);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await module.InvokeVoidAsync(
                "invokeMethodUnion",
                cancellationToken,
                [
                    target,
                    name,
                    args,
                    arms.Select(arm => arm.ToJavaScriptDescriptor()).ToArray(),
                    handlerReference,
                    "ReceiveUnionJson",
                    "ReceiveUnionReference",
                ]).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await handler.TakeResultAsync().ConfigureAwait(false);
        }
        finally
        {
            await handler.DisposeAsync().ConfigureAwait(false);
            handlerReference.Dispose();
        }
    }

    public static ValueTask<IJSObjectReference> GetIndexObjectReferenceAsync(
        IJSObjectReference module,
        IJSObjectReference target,
        object index,
        CancellationToken cancellationToken) =>
        ReceiveRequiredObjectReferenceAsync(
            module,
            "getIndexDotNetObjectReference",
            [target, index],
            cancellationToken);

    public static ValueTask<TProxy?> GetPropertyReferenceAsync<TProxy>(
        IJSObjectReference module,
        IDomProxyFactory factory,
        IJSObjectReference target,
        string name,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken)
        where TProxy : class, IDomProxy =>
        ReceiveProxyReferenceAsync<TProxy>(
            module,
            factory,
            "getPropertyDotNetObjectReference",
            [target, name],
            transport,
            cancellationToken);

    public static ValueTask<TProxy?> InvokeMethodReferenceAsync<TProxy>(
        IJSObjectReference module,
        IDomProxyFactory factory,
        IJSObjectReference target,
        string name,
        object?[]? args,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken)
        where TProxy : class, IDomProxy =>
        ReceiveProxyReferenceAsync<TProxy>(
            module,
            factory,
            "invokeMethodDotNetObjectReference",
            [target, name, args],
            transport,
            cancellationToken);

    public static ValueTask<TProxy?> GetIndexReferenceAsync<TProxy>(
        IJSObjectReference module,
        IDomProxyFactory factory,
        IJSObjectReference target,
        int index,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken)
        where TProxy : class, IDomProxy =>
        ReceiveProxyReferenceAsync<TProxy>(
            module,
            factory,
            "getIndexDotNetObjectReference",
            [target, index],
            transport,
            cancellationToken);

    public static async ValueTask InvokeReferenceCallbackAsync<TProxy>(
        IJSObjectReference module,
        IDomProxyFactory factory,
        IJSObjectReference target,
        string name,
        int callbackArgumentIndex,
        object?[]? args,
        DomTransportDescriptor transport,
        Func<DomBorrowedReference<TProxy>?, Task> callback,
        CancellationToken cancellationToken)
        where TProxy : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(transport);
        transport.RequireReference(nameof(transport));

        var prepared = args ?? [];
        if (callbackArgumentIndex < 0 || callbackArgumentIndex > prepared.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(callbackArgumentIndex),
                callbackArgumentIndex,
                "The callback index must identify an insertion point in the argument list.");
        }

        var handler = new DomReferenceCallbackHandler<TProxy>(
            factory,
            transport,
            callback);
        var handlerReference = DotNetObjectReference.Create(handler);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await module.InvokeVoidAsync(
                "invokeMethodReferenceCallback",
                cancellationToken,
                [
                    target,
                    name,
                    prepared,
                    callbackArgumentIndex,
                    handlerReference,
                    "HandleReference",
                ]).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            handler.Dispose();
            handlerReference.Dispose();
        }
    }

    public static async ValueTask<TResult>
        InvokeReferenceResultCallbackAsync<TProxy, TResult>(
            IJSObjectReference module,
            IDomProxyFactory factory,
            IJSObjectReference target,
            string name,
            int callbackArgumentIndex,
            object?[]? args,
            DomTransportDescriptor transport,
            Func<DomBorrowedReference<TProxy>?, Task<TResult>> callback,
            CancellationToken cancellationToken)
        where TProxy : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(transport);
        transport.RequireReference(nameof(transport));

        var prepared = args ?? [];
        if (callbackArgumentIndex < 0 || callbackArgumentIndex > prepared.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(callbackArgumentIndex),
                callbackArgumentIndex,
                "The callback index must identify an insertion point in the argument list.");
        }

        var handler = new DomReferenceResultCallbackHandler<TProxy, TResult>(
            factory,
            transport,
            callback);
        var handlerReference = DotNetObjectReference.Create(handler);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await module.InvokeAsync<TResult>(
                "invokeMethodReferenceResultCallback",
                cancellationToken,
                [
                    target,
                    name,
                    prepared,
                    callbackArgumentIndex,
                    handlerReference,
                    "HandleReferenceResult",
                ]).ConfigureAwait(false);
        }
        finally
        {
            handler.Dispose();
            handlerReference.Dispose();
        }
    }

    public static async ValueTask<DomCallbackConstruction>
        ConstructReferencePairCallbackAsync<TFirst, TSecond>(
            IJSObjectReference module,
            IDomProxyFactory factory,
            string constructorPath,
            int callbackArgumentIndex,
            object?[]? args,
            DomTransportDescriptor firstTransport,
            DomTransportDescriptor secondTransport,
            Func<
                DomBorrowedReference<TFirst>,
                DomBorrowedReference<TSecond>,
                Task> callback,
            CancellationToken cancellationToken)
        where TFirst : class, IDomProxy
        where TSecond : class, IDomProxy
    {
        var handler = new DomReferencePairCallbackHandler<TFirst, TSecond>(
            factory,
            firstTransport,
            secondTransport,
            callback);
        var handlerReference = DotNetObjectReference.Create(handler);
        try
        {
            var reference = await module.InvokeAsync<IJSObjectReference>(
                "constructReferencePairCallback",
                cancellationToken,
                [
                    constructorPath,
                    args,
                    callbackArgumentIndex,
                    handlerReference,
                    "HandleReferencePair",
                ]).ConfigureAwait(false);
            return new DomCallbackConstruction(
                reference,
                new CallbackResource(handler, handlerReference));
        }
        catch
        {
            handler.Dispose();
            handlerReference.Dispose();
            throw;
        }
    }

    private sealed class CallbackResource(
        IDisposable handler,
        IDisposable handlerReference) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            handler.Dispose();
            handlerReference.Dispose();
        }
    }

    public static async ValueTask<DomReferenceEventSubscription<TProxy>>
        AddReferenceEventListenerAsync<TProxy>(
            IJSObjectReference module,
            IDomProxyFactory factory,
            IJSObjectReference target,
            string type,
            DomTransportDescriptor transport,
            DomEventListenerOptions? options,
            Func<DomBorrowedReference<TProxy>, Task> callback,
            CancellationToken cancellationToken)
        where TProxy : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(transport);
        transport.RequireReference(nameof(transport));
        if (transport.Nullable)
        {
            throw new ArgumentException(
                "DOM event references are not nullable.",
                nameof(transport));
        }
        ArgumentNullException.ThrowIfNull(callback);

        var handler = new DomReferenceCallbackHandler<TProxy>(
            factory,
            transport,
            value => callback(value ?? throw new DomTransportException(
                $"JavaScript supplied null for event type '{transport.SourceType}'.")));
        var handlerReference = DotNetObjectReference.Create(handler);
        var registrationHandler = new DomEventRegistrationHandler();
        var registrationHandlerReference =
            DotNetObjectReference.Create(registrationHandler);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await module.InvokeVoidAsync(
                "addDotNetReferenceEventListener",
                cancellationToken,
                [
                    target,
                    type,
                    handlerReference,
                    "HandleReference",
                    registrationHandlerReference,
                    "ReceiveRegistration",
                    options?.ToInteropValue(),
                ])
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var registration = registrationHandler.TakeRegistration();
            return new DomReferenceEventSubscription<TProxy>(
                registration,
                handlerReference);
        }
        catch (Exception registrationFailure)
        {
            try
            {
                await registrationHandler.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    "Typed event registration failed and rollback also failed.",
                    registrationFailure,
                    cleanupFailure);
            }
            finally
            {
                handler.Dispose();
                handlerReference.Dispose();
            }
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(registrationFailure)
                .Throw();
            throw;
        }
        finally
        {
            registrationHandlerReference.Dispose();
        }
    }

    public static async ValueTask<DomReadStream> OpenReadStreamAsync(
        IJSObjectReference module,
        IJSObjectReference reference,
        DomTransportDescriptor transport,
        long maximumLength,
        CancellationToken cancellationToken)
    {
        var stream = await ReceiveStreamAsync(
            module,
            "createDotNetStreamReference",
            [reference],
            transport,
            maximumLength,
            allowNull: false,
            cancellationToken).ConfigureAwait(false);
        return stream ?? throw new DomTransportException(
            $"JavaScript supplied null for non-nullable stream type '{transport.SourceType}'.");
    }

    public static async ValueTask<DomReadStream> InvokeMethodStreamAsync(
        IJSObjectReference module,
        IJSObjectReference reference,
        string name,
        object?[]? args,
        DomTransportDescriptor transport,
        long maximumLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(transport);
        if (transport.Nullable)
        {
            throw new ArgumentException(
                "Nullable stream results must use InvokeMethodNullableStreamAsync.",
                nameof(transport));
        }
        var stream = await ReceiveStreamAsync(
            module,
            "invokeMethodDotNetStreamReference",
            [reference, name, args],
            transport,
            maximumLength,
            allowNull: false,
            cancellationToken).ConfigureAwait(false);
        return stream ?? throw new DomTransportException(
            $"JavaScript supplied null for non-nullable stream type '{transport.SourceType}'.");
    }

    public static ValueTask<DomReadStream?> InvokeMethodNullableStreamAsync(
        IJSObjectReference module,
        IJSObjectReference reference,
        string name,
        object?[]? args,
        DomTransportDescriptor transport,
        long maximumLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(transport);
        if (!transport.Nullable)
        {
            throw new ArgumentException(
                "Nullable stream results require nullable transport metadata.",
                nameof(transport));
        }
        return ReceiveStreamAsync(
            module,
            "invokeMethodDotNetStreamReference",
            [reference, name, args],
            transport,
            maximumLength,
            allowNull: true,
            cancellationToken);
    }

    private static async ValueTask<DomReadStream?> ReceiveStreamAsync(
        IJSObjectReference module,
        string identifier,
        object?[] args,
        DomTransportDescriptor transport,
        long maximumLength,
        bool allowNull,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(transport);
        transport.RequireStreamable(nameof(transport));
        if (maximumLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumLength),
                maximumLength,
                "Maximum stream length cannot be negative.");
        }

        var handler = new DomStreamCallbackHandler();
        var handlerReference = DotNetObjectReference.Create(handler);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocationArgs = new object?[args.Length + 2];
            Array.Copy(args, invocationArgs, args.Length);
            invocationArgs[^2] = handlerReference;
            invocationArgs[^1] = "ReceiveStream";
            await module.InvokeVoidAsync(
                identifier,
                cancellationToken,
                invocationArgs).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var result = handler.TakeResult();
            if (!result.HasValue)
            {
                if (!allowNull)
                {
                    throw new DomTransportException(
                        $"JavaScript supplied null for non-nullable stream type '{transport.SourceType}'.");
                }
                return null;
            }
            var streamReference = result.Reference;
            if (streamReference is null)
            {
                return DomReadStream.Empty();
            }
            return await DomReadStream.OpenAsync(
                streamReference,
                maximumLength,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await handler.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            handlerReference.Dispose();
        }
    }

    private static async ValueTask<TProxy?> ReceiveProxyReferenceAsync<TProxy>(
        IJSObjectReference module,
        IDomProxyFactory factory,
        string identifier,
        object?[] args,
        DomTransportDescriptor transport,
        CancellationToken cancellationToken)
        where TProxy : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(transport);
        transport.RequireReference(nameof(transport));

        var reference = await ReceiveObjectReferenceAsync(
            module,
            identifier,
            args,
            transport.Nullable,
            transport.SourceType,
            cancellationToken).ConfigureAwait(false);
        if (reference is null)
        {
            return null;
        }

        try
        {
            return factory.Create<TProxy>(reference);
        }
        catch
        {
            await DomReferenceDeliveryHandler
                .DisposeReferenceAsync(reference)
                .ConfigureAwait(false);
            throw;
        }
    }

    private static async ValueTask<IJSObjectReference>
        ReceiveRequiredObjectReferenceAsync(
            IJSObjectReference module,
            string identifier,
            object?[] args,
            CancellationToken cancellationToken)
    {
        return await ReceiveObjectReferenceAsync(
            module,
            identifier,
            args,
            allowNull: false,
            sourceType: nameof(IJSObjectReference),
            cancellationToken).ConfigureAwait(false) ??
            throw new InvalidOperationException(
                "A required JavaScript object-reference delivery returned no reference.");
    }

    private static async ValueTask<IJSObjectReference?> ReceiveObjectReferenceAsync(
        IJSObjectReference module,
        string identifier,
        object?[] args,
        bool allowNull,
        string sourceType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(module);
        var handler = new DomReferenceDeliveryHandler();
        var handlerReference = DotNetObjectReference.Create(handler);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocationArgs = new object?[args.Length + 2];
            Array.Copy(args, invocationArgs, args.Length);
            invocationArgs[^2] = handlerReference;
            invocationArgs[^1] = "ReceiveReference";
            await module.InvokeVoidAsync(
                identifier,
                cancellationToken,
                invocationArgs).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var reference = handler.TakeReference();
            if (reference is null && !allowNull)
            {
                throw new DomTransportException(
                    $"JavaScript supplied null for non-nullable '{sourceType}'.");
            }
            return reference;
        }
        catch
        {
            await handler.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            handlerReference.Dispose();
        }
    }
}
