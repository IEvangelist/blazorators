// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal static class NodeExtensions
{
    internal static IEnumerable<INode> GetDescendants(this INode node, bool includeSelf = true)
    {
        if (includeSelf) yield return node;

        foreach (var descendant in node.Children ?? Enumerable.Empty<INode>())
        {
            foreach (var child in descendant.GetDescendants())
            {
                yield return child;
            }
        }
    }

    internal static IEnumerable<INode> GetAncestors(this INode node)
    {
        var current = node.Parent;

        while (current is not null)
        {
            yield return current;
            current = current.Parent;
        }
    }

    internal static IEnumerable<Node> OfKind(this IEnumerable<Node> nodes, SyntaxKind kind)
    {
        foreach (var node in nodes.Where(node => node.Kind == kind))
        {
            yield return node;
        }
    }

    internal static IEnumerable<INode> OfKind(this IEnumerable<INode> nodes, SyntaxKind kind)
    {
        foreach (var node in nodes.Where(node => node.Kind == kind))
        {
            yield return node;
        }
    }
}