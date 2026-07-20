// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>Thrown when a value has no safe, explicit JS interop transport.</summary>
public sealed class DomTransportException : InvalidOperationException
{
    /// <summary>Creates a transport exception with a precise failure message.</summary>
    public DomTransportException(string message) : base(message)
    {
    }
}
