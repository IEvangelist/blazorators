// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class EmptyStatement : Statement
{
    public EmptyStatement()
    {
        Kind = TypeScriptSyntaxKind.EmptyStatement;
    }
}