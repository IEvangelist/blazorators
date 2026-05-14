// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators;

/// <summary>
/// A value-equatable snapshot of a <see cref="Location"/> suitable for the
/// Roslyn incremental-generator cache. The CLR <see cref="Location"/> type
/// holds references to <see cref="SyntaxTree"/> instances which are not
/// value-equatable, so projecting locations to this record allows the
/// pipeline to compare them by content rather than identity.
/// </summary>
internal sealed record LocationInfo(
    string FilePath,
    TextSpan TextSpan,
    LinePositionSpan LineSpan)
{
    public Location ToLocation() =>
        Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo? CreateFrom(SyntaxNode node) =>
        CreateFrom(node.GetLocation());

    public static LocationInfo? CreateFrom(Location location)
    {
        if (location.SourceTree is null)
        {
            return null;
        }

        return new LocationInfo(
            location.SourceTree.FilePath,
            location.SourceSpan,
            location.GetLineSpan().Span);
    }
}
