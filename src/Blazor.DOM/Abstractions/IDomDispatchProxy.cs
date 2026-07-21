// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>
/// Runtime dependencies exposed to generated default interface implementations.
/// </summary>
public interface IDomDispatchProxy : IDomEventTargetProxy
{
    /// <summary>The host dispatch runtime.</summary>
    IDomRuntime DispatchRuntime { get; }

    /// <summary>The generated proxy factory.</summary>
    IDomProxyFactory DispatchFactory { get; }
}
