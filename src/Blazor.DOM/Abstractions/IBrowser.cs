// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Provides lazy access to live browser global objects.  Do not inject or use
/// this service during Blazor prerendering; the first call will fail clearly if
/// JavaScript interop is unavailable.
/// </summary>
public interface IBrowser : IAsyncDisposable
{
    /// <summary>Gets a JS reference to the global <c>window</c> object.</summary>
    ValueTask<IJSObjectReference> GetWindowAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a JS reference to <c>window.document</c>.</summary>
    ValueTask<IJSObjectReference> GetDocumentAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a JS reference to <c>window.navigator</c>.</summary>
    ValueTask<IJSObjectReference> GetNavigatorAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a dotted global path (e.g. <c>"navigator.geolocation"</c>) and
    /// returns a JS reference to the resolved object.
    /// </summary>
    /// <param name="path">Dotted path from <c>window</c>.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    ValueTask<IJSObjectReference> GetGlobalAsync(
        string path, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a browser global or nested entry point is available.</summary>
    ValueTask<bool> IsGlobalAvailableAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>Resolves an authoritative global path as an owned typed proxy.</summary>
    ValueTask<TProxy> GetGlobalAsync<TProxy>(
        string path,
        CancellationToken cancellationToken = default)
        where TProxy : class, IDomProxy;
}
