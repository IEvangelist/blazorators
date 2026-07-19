// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Blazor Server implementation of <see cref="IBrowser"/>.  Resolves live JS
/// references to common browser globals via the <see cref="IDomRuntime"/>.
/// Does not cache references; callers own returned references.
/// </summary>
internal sealed class ServerBrowser(IDomRuntime runtime) : IBrowser, IDisposable
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
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public void Dispose() { /* No managed resources to release. */ }
}
