// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public interface INode : ITextRange
{
    public TypeScriptSyntaxKind Kind { get; set; }
    public NodeFlags Flags { get; set; }
    public ModifierFlags ModifierFlagsCache { get; set; }
    public TransformFlags TransformFlags { get; set; }
    public NodeArray<Decorator> Decorators { get; set; }
    public NodeArray<Modifier> Modifiers { get; set; }
    public int Id { get; set; }
    public INode? Parent { get; set; }
    public List<Node>? Children { get; set; }
    public int Depth { get; set; }
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
    public TsType ContextualType { get; set; }
    public TypeMapper ContextualMapper { get; set; }
    public int TagInt { get; set; }
    public Node First { get; }
    public Node Last { get; }
    public int Count { get; }

    public string? GetText(string? source = null);
    public string? GetTextWithComments(string? source = null);
    public string? GetTreeString(bool withPos = true);
    public string ToString(bool withPos);
}