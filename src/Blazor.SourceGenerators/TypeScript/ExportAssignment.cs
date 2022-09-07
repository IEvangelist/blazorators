// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ExportAssignment : DeclarationStatement
{
    internal ExportAssignment() => ((INode)this).Kind = CommentKind.ExportAssignment;

    internal bool IsExportEquals { get; set; }
    internal IExpression Expression { get; set; }
}