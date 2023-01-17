// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ImportDeclaration : Statement
{
    public ImportDeclaration()
    {
        Kind = TypeScriptSyntaxKind.ImportDeclaration;
    }

    public ImportClause ImportClause { get; set; }
    public IExpression ModuleSpecifier { get; set; }
}