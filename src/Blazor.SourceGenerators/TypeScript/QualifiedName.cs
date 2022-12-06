// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class QualifiedName : Node, IEntityName
{
    public QualifiedName() => ((INode)this).Kind = TypeScriptSyntaxKind.QualifiedName;

    public IEntityName Left { get; set; }
    public Identifier Right { get; set; }
}
