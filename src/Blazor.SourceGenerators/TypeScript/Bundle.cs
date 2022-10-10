// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class Bundle : Node
{
    internal Bundle() => ((INode)this).Kind = SyntaxKind.Bundle;

    internal SourceFile[] SourceFiles { get; set; } = default!;
}