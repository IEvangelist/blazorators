// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

internal sealed class BrowserArrayDomProxy<T>(
    IJSObjectReference reference,
    IDomRuntime runtime,
    IDomProxyFactory factory)
    : DomProxyBase(reference, runtime, factory), IBrowserArray<T>
{
    public ValueTask<int> GetLengthAsync(
        CancellationToken cancellationToken = default) =>
        DomDispatch.GetPropertyAsync<int>(
            this,
            "length",
            DomTransportDescriptor.JsonValue("number"),
            cancellationToken);

    public ValueTask<T> GetAsync(
        int index,
        CancellationToken cancellationToken = default) =>
        DomDispatch.GetIndexAsync<T>(
            this,
            index,
            DomDispatch.InferTransport<T>("Array element"),
            cancellationToken);

    public ValueTask SetAsync(
        int index,
        T value,
        CancellationToken cancellationToken = default) =>
        DomDispatch.SetIndexAsync(this, index, value, cancellationToken);
}
