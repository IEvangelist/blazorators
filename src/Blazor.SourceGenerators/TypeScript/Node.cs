// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

/// <summary>
/// Represents a TypeScript node as a <see cref="TextRange"/>.
/// </summary>
public class Node : TextRange, INode
{
    TypeScriptSyntaxKind INode.Kind { get; set; }
    NodeFlags INode.Flags { get; set; }
    ModifierFlags INode.ModifierFlagsCache { get; set; }
    TransformFlags INode.TransformFlags { get; set; }
    NodeArray<Decorator> INode.Decorators? { get; set; }
    NodeArray<Modifier> INode.Modifiers? { get; set; }
    int INode.Id { get; set; }
    INode? INode.Parent { get; set; }
    List<Node>? INode.Children { get; set; }
    int INode.Depth { get; set; }
    Node INode.Original? { get; set; }
    bool INode.StartsOnNewLine { get; set; }
    List<JsDoc> INode.JsDoc? { get; set; }
    List<JsDoc> INode.JsDocCache? { get; set; }
    Symbol INode.Symbol? { get; set; }
    SymbolTable INode.Locals? { get; set; }
    Node INode.NextContainer? { get; set; }
    Symbol INode.LocalSymbol? { get; set; }
    FlowNode INode.FlowNode? { get; set; }
    EmitNode INode.EmitNode? { get; set; }
    TsType INode.ContextualType? { get; set; }
    TypeMapper INode.ContextualMapper? { get; set; }
    int INode.TagInt { get; set; }
    Node INode.First { get; } = default!;
    Node INode.Last { get; } = default!;
    int INode.Count { get; }

    /// <summary>
    /// Gets the node's implementation of the <see cref="ISourceAbstractSyntaxTree" />.
    /// </summary>
    public ISourceAbstractSyntaxTree AbstractSyntaxTree? { get; set; }

    /// <summary>
    /// Gets the source string from the underlying <see cref="AbstractSyntaxTree" /> as its raw value.
    /// </summary>
    public string SourceStr => AbstractSyntaxTree.SourceStr;

    /// <summary>
    /// Gets an identifier string that represents the node.
    /// </summary>
    public string? IdentifierStr => ((INode)this).Kind == TypeScriptSyntaxKind.Identifier
        ? ((INode)this).GetText()
        : ((INode)this).Children
            ?.Cast<INode>()
            ?.FirstOrDefault(node => node.Kind is TypeScriptSyntaxKind.Identifier)
            ?.GetText()
            ?.Trim();

    /// <summary>
    /// Gets the node's parent identifier.
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>
    /// Defaults to -1, otherwise, when a zero or greater value, represents the start of the node.
    /// </summary>
    public int NodeStart { get; set; } = -1;

    public void MakeChildren(SourceAbstractSyntaxTree ast)
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
        return $"{Enum.GetName(typeof(TypeScriptSyntaxKind), ((INode)this).Kind)}  {posStr} {IdentifierStr}";
    }

    public Node? First => ((INode)this).Children.FirstOrDefault();
    public Node? Last => ((INode)this).Children.LastOrDefault();
    public int Count => ((INode)this).Children?.Count ?? 0;

    public IEnumerable<Node> OfKind(TypeScriptSyntaxKind kind) =>
        GetDescendants(false).OfKind(kind);

    public IEnumerable<Node> GetDescendants(bool includeSelf = true)
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
        INode node = this;

        if (NodeStart is -1)
        {
            return node.Pos.HasValue && node.End.HasValue
                ? source.SubString(node.Pos.Value, node.End.Value)
                : null;
        }

        return node.End.HasValue ? source.SubString(NodeStart, node.End.Value) : null;
    }

    string? INode.GetTextWithComments(string? source)
    {
        source ??= SourceStr;
        INode node = this;

        return node.Pos.HasValue && node.End.HasValue
            ? source.SubString(node.Pos.Value, node.End.Value)
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
            return $"{Enum.GetName(typeof(TypeScriptSyntaxKind), ((INode)this).Kind)}  {posStr} {IdentifierStr}";
        }

        return $"{Enum.GetName(typeof(TypeScriptSyntaxKind), ((INode)this).Kind)}  {IdentifierStr}";
    }
}
