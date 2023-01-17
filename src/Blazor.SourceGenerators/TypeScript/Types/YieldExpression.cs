// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class YieldExpression : Expression
{
    public YieldExpression()
    {
        Kind = TypeScriptSyntaxKind.YieldExpression;
    }

    public AsteriskToken AsteriskToken { get; set; }
    public IExpression Expression { get; set; }
}