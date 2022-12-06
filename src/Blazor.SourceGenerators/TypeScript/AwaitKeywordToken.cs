// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class AwaitKeywordToken : Token
{
    public AwaitKeywordToken() => ((INode)this).Kind = TypeScriptSyntaxKind.AwaitKeyword;
}
