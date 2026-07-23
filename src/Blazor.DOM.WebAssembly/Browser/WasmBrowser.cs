// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Blazor WebAssembly implementation of <see cref="IBrowser"/>.
/// </summary>
internal sealed class WasmBrowser(
    IDomRuntime runtime,
    IDomProxyFactory proxyFactory) : IBrowser, IDisposable
{
    /// <inheritdoc />
    public ValueTask<IJSObjectReference> GetWindowAsync(CancellationToken cancellationToken = default) =>
        runtime.GetGlobalAsync("window", cancellationToken);

    /// <inheritdoc />
    public ValueTask<IJSObjectReference> GetDocumentAsync(CancellationToken cancellationToken = default) =>
        runtime.GetGlobalAsync("document", cancellationToken);

    /// <inheritdoc />
    public ValueTask<IJSObjectReference> GetNavigatorAsync(CancellationToken cancellationToken = default) =>
        runtime.GetGlobalAsync("navigator", cancellationToken);

    /// <inheritdoc />
    public ValueTask<IJSObjectReference> GetGlobalAsync(
        string path, CancellationToken cancellationToken = default) =>
        runtime.GetGlobalAsync(path, cancellationToken);

    /// <inheritdoc />
    public ValueTask<bool> IsGlobalAvailableAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        runtime.IsGlobalAvailableAsync(path, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<TProxy> GetGlobalAsync<TProxy>(
        string path,
        CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy =>
        proxyFactory.Create<TProxy>(
            await runtime.GetGlobalAsync(path, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public void Dispose() { /* No managed resources to release. */ }
}
