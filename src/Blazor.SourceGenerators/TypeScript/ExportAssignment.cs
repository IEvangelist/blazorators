// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ExportAssignment : DeclarationStatement
{
    internal ExportAssignment() => ((INode)this).Kind = SyntaxKind.ExportAssignment;

    internal bool IsExportEquals { get; set; }
    internal IExpression Expression { get; set; }
}