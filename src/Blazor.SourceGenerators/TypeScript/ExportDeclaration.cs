// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ExportDeclaration : DeclarationStatement
{
    internal ExportDeclaration() => ((INode)this).Kind = SyntaxKind.ExportDeclaration;

    internal NamedExports ExportClause { get; set; }
    internal IExpression ModuleSpecifier { get; set; }
}