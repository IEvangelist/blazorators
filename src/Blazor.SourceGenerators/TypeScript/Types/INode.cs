// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable

// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public interface INode : ITextRange
{
    TypeScriptSyntaxKind Kind { get; set; }
    NodeFlags Flags { get; set; }
    ModifierFlags ModifierFlagsCache { get; set; }
    TransformFlags TransformFlags { get; set; }
    NodeArray<Decorator> Decorators { get; set; }
    NodeArray<Modifier> Modifiers { get; set; }
    int Id { get; set; }
    INode Parent { get; set; }
    List<Node> Children { get; set; }
    int Depth { get; set; }
    Node Original { get; set; }
    bool StartsOnNewLine { get; set; }
    List<JsDoc> JsDoc { get; set; }
    List<JsDoc> JsDocCache { get; set; }
    Symbol Symbol { get; set; }
    SymbolTable Locals { get; set; }
    Node NextContainer { get; set; }
    Symbol LocalSymbol { get; set; }
    FlowNode FlowNode { get; set; }
    EmitNode EmitNode { get; set; }
    TypeScriptType ContextualType { get; set; }
    TypeMapper ContextualMapper { get; set; }
    int TagInt { get; set; }
    string GetText(string source = null);
    string GetTextWithComments(string source = null);
    string GetTreeString(bool withPos = true);
    string ToString(bool withPos);
    Node First { get; }
    Node Last { get; }
    int Count { get; }
}