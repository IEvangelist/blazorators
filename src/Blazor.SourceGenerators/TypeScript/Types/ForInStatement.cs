// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ForInStatement : IterationStatement
{
    public ForInStatement()
    {
        Kind = TypeScriptSyntaxKind.ForInStatement;
    }

    public IVariableDeclarationListOrExpression Initializer { get; set; }
    public IExpression Expression { get; set; }
}