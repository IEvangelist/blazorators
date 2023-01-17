// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ForStatement : IterationStatement
{
    public ForStatement()
    {
        Kind = TypeScriptSyntaxKind.ForStatement;
    }

    public IVariableDeclarationListOrExpression Initializer { get; set; }
    public IExpression Condition { get; set; }
    public IExpression Incrementor { get; set; }
}