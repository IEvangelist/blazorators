// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.DOM.Tests.Fakes;

public sealed class FakeJSStreamReference(long length, Stream stream) : IJSStreamReference
{
    public long Length { get; } = length;

    public Stream Stream { get; } = stream;

    public long? MaximumLength { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public int OpenCallCount { get; private set; }

    public int DisposeCallCount { get; private set; }

    public Exception? OpenException { get; set; }

    public ValueTask<Stream> OpenReadStreamAsync(
        long maxAllowedSize = 512_000,
        CancellationToken cancellationToken = default)
    {
        OpenCallCount++;
        MaximumLength = maxAllowedSize;
        CancellationToken = cancellationToken;
        if (OpenException is not null)
        {
            return ValueTask.FromException<Stream>(OpenException);
        }
        if (Length > maxAllowedSize)
        {
            return ValueTask.FromException<Stream>(
                new IOException(
                    $"Stream length {Length} exceeds maximum {maxAllowedSize}."));
        }
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<Stream>(cancellationToken);
        }
        return ValueTask.FromResult(Stream);
    }

    public ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        return ValueTask.CompletedTask;
    }
}
