// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators;

/// <summary>
/// Value-equatable projection of an interface decorated with one of the
/// interop attributes. The Roslyn syntax-provider transform produces this
/// record so that downstream pipeline steps can be cached by content
/// rather than by syntax-node identity.
/// </summary>
internal sealed record InteropTarget(
    GeneratorOptions Options,
    string InterfaceName,
    bool IsPartial,
    string? ContainingNamespace,
    bool IsGeneric,
    LocationInfo? IdentifierLocation,
    LocationInfo? AttributeLocation);
