// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ColonToken : Token
{
    public ColonToken() => ((INode)this).Kind = TypeScriptSyntaxKind.ColonToken;
}
