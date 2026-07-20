// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.ExceptionServices;

namespace Microsoft.JSInterop;

/// <summary>
/// Owns a bounded .NET stream and any originating <see cref="IJSStreamReference"/>.
/// </summary>
public sealed class DomReadStream : IAsyncDisposable
{
    private readonly IJSStreamReference? _reference;
    private int _disposed;

    private DomReadStream(
        IJSStreamReference? reference,
        Stream stream,
        long length)
    {
        _reference = reference;
        Stream = stream;
        Length = length;
    }

    /// <summary>The byte length reported by JavaScript.</summary>
    public long Length { get; }

    /// <summary>The bounded readable stream.</summary>
    public Stream Stream { get; }

    internal static async ValueTask<DomReadStream> OpenAsync(
        IJSStreamReference reference,
        long maximumLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (maximumLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumLength),
                maximumLength,
                "Maximum stream length cannot be negative.");
        }

        try
        {
            var stream = await reference
                .OpenReadStreamAsync(maximumLength, cancellationToken)
                .ConfigureAwait(false);
            return new DomReadStream(reference, stream, reference.Length);
        }
        catch
        {
            await DisposeReferenceAsync(reference).ConfigureAwait(false);
            throw;
        }
    }

    internal static DomReadStream Empty() =>
        new(
            reference: null,
            new MemoryStream(Array.Empty<byte>(), writable: false),
            length: 0);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Exception? failure = null;
        try
        {
            await Stream.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        if (_reference is not null)
        {
            try
            {
                await DisposeReferenceAsync(_reference).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failure ??= ex;
            }
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static async ValueTask DisposeReferenceAsync(IJSStreamReference reference)
    {
        try
        {
            await reference.DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }
}
