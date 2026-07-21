// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Owns an inbound live reference whose exact union arm cannot be branded safely.
/// The caller may transfer it once into one of the generated strongly typed candidate views.
/// </summary>
public sealed class DomUnclassifiedReference : IAsyncDisposable
{
    private IJSObjectReference? _reference;

    private DomUnclassifiedReference(IJSObjectReference reference) =>
        _reference = reference ?? throw new ArgumentNullException(nameof(reference));

    public static DomUnclassifiedReference Owned(IJSObjectReference reference) =>
        new(reference);

    /// <summary>
    /// Atomically transfers ownership into the selected typed proxy. No runtime arm is guessed.
    /// </summary>
    public TProxy TakeAs<TProxy>(IDomProxyFactory proxyFactory)
        where TProxy : class, IDomProxy
    {
        ArgumentNullException.ThrowIfNull(proxyFactory);
        var reference = Interlocked.Exchange(ref _reference, null)
            ?? throw new InvalidOperationException(
                "The unclassified reference was already transferred or disposed.");
        try
        {
            return proxyFactory.Create<TProxy>(reference);
        }
        catch
        {
            _reference = reference;
            throw;
        }
    }

    /// <summary>
    /// Atomically transfers ownership through a strongly typed candidate factory.
    /// </summary>
    public TContract TakeAs<TContract>(
        Func<IJSObjectReference, TContract> candidateFactory)
    {
        ArgumentNullException.ThrowIfNull(candidateFactory);
        var reference = Interlocked.Exchange(ref _reference, null)
            ?? throw new InvalidOperationException(
                "The unclassified reference was already transferred or disposed.");
        try
        {
            return candidateFactory(reference);
        }
        catch
        {
            _reference = reference;
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        var reference = Interlocked.Exchange(ref _reference, null);
        if (reference is not null)
            await reference.DisposeAsync().ConfigureAwait(false);
    }
}
