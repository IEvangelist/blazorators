// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class AsteriskToken : Token
{
    public AsteriskToken()
    {
        Kind = TypeScriptSyntaxKind.AsteriskToken;
    }
}