// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface INode : ITextRange
{
    internal SyntaxKind Kind { get; set; }
    internal NodeFlags Flags { get; set; }
    internal ModifierFlags ModifierFlagsCache { get; set; }
    internal TransformFlags TransformFlags { get; set; }
    internal NodeArray<Decorator> Decorators { get; set; }
    internal NodeArray<Modifier> Modifiers { get; set; }
    internal int Id { get; set; }
    internal INode? Parent { get; set; }
    internal List<Node>? Children { get; set; }
    internal int Depth { get; set; }
    internal Node Original { get; set; }
    internal bool StartsOnNewLine { get; set; }
    internal List<JsDoc> JsDoc { get; set; }
    internal List<JsDoc> JsDocCache { get; set; }
    internal Symbol Symbol { get; set; }
    internal SymbolTable Locals { get; set; }
    internal Node NextContainer { get; set; }
    internal Symbol LocalSymbol { get; set; }
    internal FlowNode FlowNode { get; set; }
    internal EmitNode EmitNode { get; set; }
    internal TsType ContextualType { get; set; }
    internal TypeMapper ContextualMapper { get; set; }
    internal int TagInt { get; set; }
    internal Node First { get; }
    internal Node Last { get; }
    internal int Count { get; }

    internal string? GetText(string? source = null);
    internal string? GetTextWithComments(string? source = null);
    internal string? GetTreeString(bool withPos = true);
    internal string ToString(bool withPos);
}