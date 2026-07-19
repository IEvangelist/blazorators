// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Represents a live DOM proxy backed by a JS object reference.
/// Generated DOM types implement this interface.
/// </summary>
public interface IDomProxy : IAsyncDisposable
{
    /// <summary>The underlying JS object reference for this proxy.</summary>
    IJSObjectReference Reference { get; }
}
