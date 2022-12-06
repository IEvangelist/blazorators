// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class EqualsToken : Token
{
    public EqualsToken() => ((INode)this).Kind = TypeScriptSyntaxKind.EqualsToken;
}
