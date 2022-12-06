// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class Bundle : Node
{
    public Bundle() => ((INode)this).Kind = TypeScriptSyntaxKind.Bundle;

    public SourceFile[] SourceFiles? { get; set; }
}