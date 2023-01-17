// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ExportDeclaration : DeclarationStatement
{
    public ExportDeclaration()
    {
        Kind = TypeScriptSyntaxKind.ExportDeclaration;
    }

    public NamedExports ExportClause { get; set; }
    public IExpression ModuleSpecifier { get; set; }
}