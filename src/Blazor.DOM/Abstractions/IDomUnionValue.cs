// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Runtime metadata implemented by generated discriminated TypeScript union values.
/// </summary>
public interface IDomUnionValue
{
    /// <summary>The one-based generated arm index, or zero for an uninitialized value.</summary>
    int ArmIndex { get; }

    /// <summary>The authoritative transport for the selected arm.</summary>
    DomTransportDescriptor SelectedTransport { get; }
}
