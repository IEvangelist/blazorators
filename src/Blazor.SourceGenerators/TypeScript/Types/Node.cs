// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
using System.Diagnostics;
using Blazor.SourceGenerators.TypeScript.Compiler;

namespace Blazor.SourceGenerators.TypeScript.Types;

[DebuggerDisplay("{SourceText}")]
public class Node : TextRange, INode
{
    public List<Node> Children { get; set; } = new();
    public ITypeScriptAbstractSyntaxTree AbstractSyntaxTree { get; set; }

    public string RawSourceText => AbstractSyntaxTree.RawSourceText;

    public string SourceText => GetText().ToString();

    public string Identifier => Kind is TypeScriptSyntaxKind.Identifier
        ? GetText().ToString()
        : Children.FirstOrDefault(v => v.Kind is TypeScriptSyntaxKind.Identifier)
            ?.GetText()
            .Trim()
            .ToString();

    public int ParentId { get; set; }
    public int Depth { get; set; }
    public int NodeStart { get; set; } = -1;
    public TypeScriptSyntaxKind Kind { get; set; }
    public NodeFlags Flags { get; set; }
    public ModifierFlags ModifierFlagsCache { get; set; }
    public TransformFlags TransformFlags { get; set; }
    public NodeArray<Decorator> Decorators { get; set; }
    public NodeArray<Modifier> Modifiers { get; set; }
    public int Id { get; set; }
    public INode Parent { get; set; }
    public Node Original { get; set; }
    public bool StartsOnNewLine { get; set; }
    public List<JsDoc> JsDoc { get; set; }
    public List<JsDoc> JsDocCache { get; set; }
    public Symbol Symbol { get; set; }
    public SymbolTable Locals { get; set; }
    public Node NextContainer { get; set; }
    public Symbol LocalSymbol { get; set; }
    public FlowNode FlowNode { get; set; }
    public EmitNode EmitNode { get; set; }
    public TypeScriptType ContextualType { get; set; }
    public TypeMapper ContextualMapper { get; set; }
    public int TagInt { get; set; }

    public void ParseChildren(ITypeScriptAbstractSyntaxTree abstractSyntaxTree) =>
        Ts.ForEachChild(this, node =>
        {
            if (node is null) return null;
            var n = (Node)node;
            n.AbstractSyntaxTree = abstractSyntaxTree;
            n.Depth = Depth + 1;
            n.Parent = this;
            if (n.Pos.HasValue)
            {
                n.NodeStart = Scanner.SkipTriviaM(RawSourceText, n.Pos.Value);
            }
            Children.Add(n);
            n.ParseChildren(abstractSyntaxTree);
            return null;
        });

    public ReadOnlySpan<char> GetText(string source = null)
    {
        source ??= RawSourceText;

        var (start, end) = (Pos ?? NodeStart, End.Value);

        return start is 0 || end is 0
            ? null
            : source.AsSpan(start, end - start);
    }

    public ReadOnlySpan<char> GetTextWithComments(string source = null)
    {
        source ??= RawSourceText;

        var (start, end) =
            (Pos.GetValueOrDefault(-1), End.GetValueOrDefault(-1));

        return start is -1 || end is -1
            ? null
            : source.AsSpan(start, end - start);
    }

    public override string ToString() => ToString(true);

    public string ToString(bool withPos)
    {
        if (withPos)
        {
            var posStr = $" [{Pos}, {End}]";

            return $"{Enum.GetName(typeof(TypeScriptSyntaxKind), Kind)}  {posStr} {Identifier}";
        }
        return $"{Enum.GetName(typeof(TypeScriptSyntaxKind), Kind)}  {Identifier}";
    }
    public Node First => Children.FirstOrDefault();
    public Node Last => Children.LastOrDefault();
    public int Count => Children.Count;

    public IEnumerable<Node> OfKind(TypeScriptSyntaxKind kind) =>
        GetDescendants(false).OfKind(kind);

    public IEnumerable<Node> GetDescendants(bool includeSelf = true)
    {
        if (includeSelf) yield return this;

        foreach (var descendant in Children)
        {
            foreach (var child in descendant.GetDescendants())
                yield return child;
        }
    }

    public string GetTreeString(bool withPos = true)
    {
        var sb = new StringBuilder();
        foreach (var node in GetDescendants())
        {
            for (var i = 1; i < node.Depth; i++)
            {
                sb.Append("  ");
            }
            sb.AppendLine(node.ToString(withPos));
        }
        return sb.ToString();
    }
}
