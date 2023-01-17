// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ForOfStatement : IterationStatement
{
    public ForOfStatement()
    {
        Kind = TypeScriptSyntaxKind.ForOfStatement;
    }

    public AwaitKeywordToken AwaitModifier { get; set; }
    public IVariableDeclarationListOrExpression Initializer { get; set; }
    public IExpression Expression { get; set; }
}