// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class EndOfDeclarationMarker : Statement
{
    public EndOfDeclarationMarker()
    {
        Kind = TypeScriptSyntaxKind.EndOfDeclarationMarker;
    }
}