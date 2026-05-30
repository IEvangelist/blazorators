// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators;

/// <summary>
/// Value-equatable pipeline record produced by
/// <see cref="JavaScriptInteropGenerator"/>'s <c>[JSAutoEnum]</c> branch.
/// <para>
/// Each <see cref="EnumTarget"/> represents one C# enum projection request:
/// an interface that the consumer anchored <c>[JSAutoEnum]</c> on, along
/// with the resolved TypeScript alias name to project and the target
/// namespace.
/// </para>
/// <para>
/// Unlike <see cref="InteropTarget"/>, the anchor interface is purely a
/// discovery handle - the generator emits a sibling <c>enum</c> rather
/// than extending the interface body - so <c>partial</c> is not required
/// on the anchor (per rubber-duck design feedback for C2). The interface
/// can even be empty.
/// </para>
/// </summary>
/// <param name="TypeName">
/// The resolved TypeScript alias name to project. Either the explicit
/// <c>TypeName</c> attribute argument or the inferred value (anchor
/// interface name minus leading <c>I</c>, see
/// <see cref="OptionsInference.InferTypeName(string?)"/>).
/// </param>
/// <param name="AnchorInterfaceName">
/// The C# anchor interface identifier. Used for diagnostics and as a
/// fallback when location lookups are unavailable.
/// </param>
/// <param name="ContainingNamespace">
/// The namespace the anchor interface lives in, or <see langword="null"/>
/// when the anchor is in the global namespace.
/// </param>
/// <param name="OverrideNamespace">
/// Explicit <c>Namespace</c> attribute argument. When non-null, takes
/// precedence over <paramref name="ContainingNamespace"/>.
/// </param>
/// <param name="TypeDeclarationSources">
/// Optional list of <c>.d.ts</c> source identifiers (file basenames /
/// trailing-path segments / full paths) the consumer wants searched in
/// place of the bundled <c>lib.dom.d.ts</c>.
/// </param>
/// <param name="IdentifierLocation">
/// Source location of the anchor interface identifier. Preferred squiggle
/// target for the alias-related diagnostics (BR0006 / BR0008 / BR0009) via
/// <see cref="PreferredDiagnosticLocation"/>, since those errors are about
/// the union the anchor points at; falls back to
/// <paramref name="AttributeLocation"/> when unavailable.
/// </param>
/// <param name="AttributeLocation">
/// Source location of the <c>[JSAutoEnum]</c> attribute application. Used
/// directly for BR0001 (missing <c>TypeName</c>), since that is fixed by
/// editing the attribute itself.
/// </param>
internal sealed record EnumTarget(
    string TypeName,
    string AnchorInterfaceName,
    string? ContainingNamespace,
    string? OverrideNamespace,
    string[]? TypeDeclarationSources,
    LocationInfo? IdentifierLocation,
    LocationInfo? AttributeLocation)
{
    /// <summary>
    /// Returns the namespace the generated enum should live in. The
    /// explicit <c>Namespace</c> attribute argument wins over the
    /// anchor's containing namespace; both are nullable so the global
    /// namespace ("") is represented as <see langword="null"/>.
    /// </summary>
    public string? ResolveEffectiveNamespace() =>
        string.IsNullOrWhiteSpace(OverrideNamespace) ? ContainingNamespace : OverrideNamespace;

    /// <summary>
    /// Resolves the preferred squiggle location for alias-related
    /// diagnostics (BR0006 / BR0008 / BR0009): the anchor interface
    /// identifier when known, then the attribute, then
    /// <see cref="Location.None"/>. Pointing at the interface name keeps
    /// the error next to the thing the consumer can rename or re-anchor.
    /// </summary>
    public Location PreferredDiagnosticLocation() =>
        IdentifierLocation?.ToLocation()
            ?? AttributeLocation?.ToLocation()
            ?? Location.None;

    public bool Equals(EnumTarget? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return TypeName == other.TypeName
            && AnchorInterfaceName == other.AnchorInterfaceName
            && ContainingNamespace == other.ContainingNamespace
            && OverrideNamespace == other.OverrideNamespace
            && Equals(IdentifierLocation, other.IdentifierLocation)
            && Equals(AttributeLocation, other.AttributeLocation)
            && SourcesEqual(TypeDeclarationSources, other.TypeDeclarationSources);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = TypeName?.GetHashCode() ?? 0;
            hash = (hash * 397) ^ (AnchorInterfaceName?.GetHashCode() ?? 0);
            hash = (hash * 397) ^ (ContainingNamespace?.GetHashCode() ?? 0);
            hash = (hash * 397) ^ (OverrideNamespace?.GetHashCode() ?? 0);

            if (TypeDeclarationSources is not null)
            {
                foreach (var s in TypeDeclarationSources)
                {
                    hash = (hash * 397) ^ (s?.GetHashCode() ?? 0);
                }
            }

            return hash;
        }
    }

    private static bool SourcesEqual(string[]? a, string[]? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
        }
        return true;
    }
}
