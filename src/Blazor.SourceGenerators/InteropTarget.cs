// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators;

/// <summary>
/// Discriminator for where an <see cref="InteropTarget"/> was sourced from.
/// Used during <c>Execute</c> to break ties when the same projection is
/// requested via both pipeline paths (e.g. an
/// <c>[assembly: JSAutoService("Geolocation")]</c> alongside a
/// <c>[JSAutoInterop] partial interface IGeolocationService</c>). The
/// interface-attribute form is authoritative because it carries the
/// consumer's namespace and partial body.
/// </summary>
internal enum InteropTargetOrigin
{
    InterfaceAttribute = 0,
    AssemblyAttribute = 1,
}

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
    LocationInfo? AttributeLocation,
    InteropTargetOrigin Origin = InteropTargetOrigin.InterfaceAttribute);
