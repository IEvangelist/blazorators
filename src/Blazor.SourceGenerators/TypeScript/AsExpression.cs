// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class AsExpression : Expression
{
    public AsExpression() => ((INode)this).Kind = TypeScriptSyntaxKind.AsExpression;

    public IExpression Expression? { get; set; }
    public ITypeNode Type? { get; set; }
}