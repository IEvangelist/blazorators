// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.TypeScript.Types;

namespace Blazor.SourceGenerators.Extensions;

public static class NodeExtensions
{
    public static IEnumerable<INode> GetDescendants(
        this INode node, bool includeSelf = true)
    {
        if (includeSelf) yield return node;

        foreach (var descendant in node.Children)
            foreach (var ch in descendant.GetDescendants())
                yield return ch;
    }

    public static IEnumerable<INode> GetAncestors(this INode node)
    {
        var current = node.Parent;

        while (current != null)
        {
            yield return current;
            current = current.Parent;
        }
    }

    public static IEnumerable<Node> OfKind(
        this IEnumerable<Node> nodes, TypeScriptSyntaxKind kind)
    {
        foreach (var node in nodes)
        {
            if (node.Kind == kind) yield return node;
        }
    }

    public static IEnumerable<INode> OfKind(
        this IEnumerable<INode> nodes, TypeScriptSyntaxKind kind)
    {
        foreach (var node in nodes)
        {
            if (node.Kind == kind) yield return node;
        }
    }
}
