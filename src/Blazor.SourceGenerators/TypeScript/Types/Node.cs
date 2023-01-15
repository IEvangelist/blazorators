// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
using Blazor.SourceGenerators.TypeScript.Compiler;

namespace Blazor.SourceGenerators.TypeScript.Types;

public class Node : TextRange, INode
{
    public List<Node> Children { get; set; } = new List<Node>();
    public ITypeScriptAbstractSyntaxTree AbstractSyntaxTree { get; set; }

    public string SourceStr
    {
        get => AbstractSyntaxTree.SourceStr;
        set => AbstractSyntaxTree.SourceStr = value;
    }

    public string IdentifierStr => Kind == TypeScriptSyntaxKind.Identifier
        ? GetText()
        : Children.FirstOrDefault(v => v.Kind == TypeScriptSyntaxKind.Identifier)?.GetText().Trim();

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

    public Type ContextualType { get; set; }
    public TypeMapper ContextualMapper { get; set; }

    public int TagInt { get; set; }

    public void MakeChildren(ITypeScriptAbstractSyntaxTree abstractSyntaxTree)
    {
        Children = new List<Node>();
        Ts.ForEachChild(this, node =>
        {
            if (node == null)
                return null;
            var n = (Node)node;
            n.AbstractSyntaxTree = abstractSyntaxTree;
            n.Depth = Depth + 1;
            n.Parent = this;
            if (n.Pos != null) n.NodeStart = Scanner.SkipTriviaM(SourceStr, (int)n.Pos);
            Children.Add(n);
            n.MakeChildren(abstractSyntaxTree);
            return null;
        });
    }

    public string GetText(string source = null)
    {
        source ??= SourceStr;

        return NodeStart is -1
            ? Pos.HasValue && End.HasValue
                ? source.SubString(Pos.Value, End.Value)
                : null
            : End.HasValue ? source.SubString(NodeStart, End.Value) : null;
    }

    public string GetTextWithComments(string source = null)
    {
        source ??= SourceStr;
        return Pos != null && End != null
            ? source.Substring((int)Pos, (int)End - (int)Pos)
            : null;
    }

    public override string ToString() => ToString(true);

    public string ToString(bool withPos)
    {
        if (withPos)
        {
            var posStr = $" [{Pos}, {End}]";

            return $"{Enum.GetName(typeof(TypeScriptSyntaxKind), Kind)}  {posStr} {IdentifierStr}";
        }
        return $"{Enum.GetName(typeof(TypeScriptSyntaxKind), Kind)}  {IdentifierStr}";
    }
    public Node First => Children.FirstOrDefault();
    public Node Last => Children.LastOrDefault();
    public int Count => Children.Count;

    public IEnumerable<Node> OfKind(TypeScriptSyntaxKind kind) => GetDescendants(false).OfKind(kind);

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
