// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ExportAssignment : DeclarationStatement
{
    public ExportAssignment()
    {
        Kind = TypeScriptSyntaxKind.ExportAssignment;
    }

    public bool IsExportEquals { get; set; }
    public IExpression Expression { get; set; }
}