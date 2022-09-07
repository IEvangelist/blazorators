// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

/// <summary>
/// Represents a TypeScript node as a <see cref="TextRange"/>.
/// </summary>
internal class Node : TextRange, INode
{
    CommentKind INode.Kind { get; set; }
    NodeFlags INode.Flags { get; set; }
    ModifierFlags INode.ModifierFlagsCache { get; set; }
    TransformFlags INode.TransformFlags { get; set; }
    NodeArray<Decorator> INode.Decorators { get; set; } = default!;
    NodeArray<Modifier> INode.Modifiers { get; set; } = default!;
    int INode.Id { get; set; }
    INode? INode.Parent { get; set; }
    List<Node>? INode.Children { get; set; }
    int INode.Depth { get; set; }
    Node INode.Original { get; set; } = default!;
    bool INode.StartsOnNewLine { get; set; }
    List<JsDoc> INode.JsDoc { get; set; } = default!;
    List<JsDoc> INode.JsDocCache { get; set; } = default!;
    Symbol INode.Symbol { get; set; } = default!;
    SymbolTable INode.Locals { get; set; } = default!;
    Node INode.NextContainer { get; set; } = default!;
    Symbol INode.LocalSymbol { get; set; } = default!;
    FlowNode INode.FlowNode { get; set; } = default!;
    EmitNode INode.EmitNode { get; set; } = default!;
    TsType INode.ContextualType { get; set; } = default!;
    TypeMapper INode.ContextualMapper { get; set; } = default!;
    int INode.TagInt { get; set; }
    Node INode.First { get; } = default!;
    Node INode.Last { get; } = default!;
    int INode.Count { get; }

    /// <summary>
    /// Gets the node's implementation of the <see cref="ISourceAbstractSyntaxTree" />.
    /// </summary>
    internal ISourceAbstractSyntaxTree AbstractSyntaxTree { get; set; } = default!;

    /// <summary>
    /// Gets the source string from the underlying <see cref="AbstractSyntaxTree" /> as its raw value.
    /// </summary>
    internal string SourceStr => AbstractSyntaxTree.SourceStr;

    /// <summary>
    /// Gets an identifier string that represents the node.
    /// </summary>
    internal string? IdentifierStr => ((INode)this).Kind == SyntaxKind.Identifier
        ? ((INode)this).GetText()
        : ((INode)this).Children
            ?.Cast<INode>()
            ?.FirstOrDefault(node => node.Kind is SyntaxKind.Identifier)
            ?.GetText()
            ?.Trim();

    /// <summary>
    /// Gets the node's parent identifier.
    /// </summary>
    internal int? ParentId { get; set; }

    /// <summary>
    /// Defaults to -1, otherwise, when a zero or greater value, represents the start of the node.
    /// </summary>
    internal int NodeStart { get; set; } = -1;

    internal void MakeChildren(SourceAbstractSyntaxTree ast)
    {
        ((INode)this).Children = new List<Node>();
        Ts.ForEachChild(this, node =>
        {
            if (node is not Node n)
            {
                return null;
            }
            
            n.AbstractSyntaxTree = ast;

            var nodeInterface = (INode)n;
            nodeInterface.Depth = ((INode)this).Depth + 1;
            nodeInterface.Parent = this;
            if (nodeInterface.Pos is not null)
            {
                n.NodeStart = Scanner.SkipTriviaM(SourceStr, (int)nodeInterface.Pos);
            }

            ((INode)this).Children.Add(n);
            n.MakeChildren(ast);
            
            return null;
        });
    }

    public override string ToString()
    {
        var posStr = $" [{((INode)this).Pos}, {((INode)this).End}]";
        return $"{Enum.GetName(typeof(SyntaxKind), ((INode)this).Kind)}  {posStr} {IdentifierStr}";
    }

    internal Node? First => ((INode)this).Children.FirstOrDefault();
    internal Node? Last => ((INode)this).Children.LastOrDefault();
    internal int Count => ((INode)this).Children?.Count ?? 0;

    internal IEnumerable<Node> OfKind(SyntaxKind kind) =>
        GetDescendants(false).OfKind(kind);

    internal IEnumerable<Node> GetDescendants(bool includeSelf = true)
    {
        if (includeSelf) yield return this;

        foreach (var descendant in ((INode)this).Children ?? Enumerable.Empty<Node>())
        {
            foreach (var child in descendant.GetDescendants())
            {
                yield return child;
            }
        }
    }

    string? INode.GetText(string? source)
    {
        source ??= SourceStr;

        if (NodeStart is -1)
        {
            return ((INode)this).Pos.HasValue && ((INode)this).End.HasValue
                ? source[((INode)this).Pos.Value..((INode)this).End.Value]
                : null;
        }

        return ((INode)this).End.HasValue ? source[NodeStart..((INode)this).End.Value] : null;
    }

    string? INode.GetTextWithComments(string? source)
    {
        source ??= SourceStr;
        return ((INode)this).Pos.HasValue && ((INode)this).End.HasValue
            ? source[((INode)this).Pos.Value..((INode)this).End.Value]
            : null;
    }

    string? INode.GetTreeString(bool withPos)
    {
        var sb = new StringBuilder();
        var descendants = GetDescendants().ToList();
        foreach (var node in descendants.Cast<INode>())
        {
            for (var i = 1; i < node.Depth; i++)
            {
                sb.Append("  ");
            }
            sb.AppendLine(node.ToString(withPos));
        }
        return sb.ToString();
    }

    string INode.ToString(bool withPos)
    {
        if (withPos)
        {
            var posStr = $" [{((INode)this).Pos}, {((INode)this).End}]";
            return $"{Enum.GetName(typeof(SyntaxKind), ((INode)this).Kind)}  {posStr} {IdentifierStr}";
        }

        return $"{Enum.GetName(typeof(SyntaxKind), ((INode)this).Kind)}  {IdentifierStr}";
    }
}
