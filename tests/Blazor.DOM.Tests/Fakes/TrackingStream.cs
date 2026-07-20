// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.DOM.Tests.Fakes;

public sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
{
    public bool IsDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return base.DisposeAsync();
    }
}
