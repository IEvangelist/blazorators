// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ExportAssignment : DeclarationStatement
{
    public ExportAssignment() => ((INode)this).Kind = TypeScriptSyntaxKind.ExportAssignment;

    public bool IsExportEquals { get; set; }
    public IExpression? Expression { get; set; }
}