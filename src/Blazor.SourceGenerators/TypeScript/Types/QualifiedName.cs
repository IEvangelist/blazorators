// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class QualifiedName : Node, IEntityName
{
    public QualifiedName()
    {
        Kind = TypeScriptSyntaxKind.QualifiedName;
    }

    public IEntityName Left { get; set; }
    public Identifier Right { get; set; }
}