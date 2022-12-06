// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class DotToken : Token
{
    public DotToken() => ((INode)this).Kind = TypeScriptSyntaxKind.DotToken;
}
