// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class Bundle : Node
{
    public Bundle()
    {
        Kind = TypeScriptSyntaxKind.Bundle;
    }

    public SourceFile[] SourceFiles { get; set; }
}