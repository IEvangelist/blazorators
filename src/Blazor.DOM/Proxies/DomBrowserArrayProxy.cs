// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>Runtime proxy for live JavaScript arrays returned by focused APIs.</summary>
public sealed class DomBrowserArrayProxy<T>(
    IJSObjectReference reference,
    IDomRuntime runtime,
    IDomProxyFactory factory)
    : DomProxyBase(reference, runtime, factory), IBrowserArray<T>
{
    /// <inheritdoc />
    public ValueTask<int> GetLengthAsync(
        CancellationToken cancellationToken = default) =>
        DomDispatch.GetPropertyAsync<int>(
            this,
            "length",
            DomTransportDescriptor.JsonValue("number"),
            cancellationToken);

    /// <inheritdoc />
    public ValueTask<T> GetAsync(
        int index,
        CancellationToken cancellationToken = default) =>
        DomDispatch.GetIndexAsync<T>(
            this,
            index,
            typeof(IDomProxy).IsAssignableFrom(typeof(T))
                ? DomTransportDescriptor.JsReference(typeof(T).Name)
                : DomTransportDescriptor.JsonValue(typeof(T).Name),
            cancellationToken);

    /// <inheritdoc />
    public ValueTask SetAsync(
        int index,
        T value,
        CancellationToken cancellationToken = default) =>
        DomDispatch.SetIndexAsync(this, index, value, cancellationToken);
}
