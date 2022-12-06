// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class SpreadElement : Expression
{
    public SpreadElement() => ((INode)this).Kind = TypeScriptSyntaxKind.SpreadElement;

    public IExpression Expression? { get; set; }
}