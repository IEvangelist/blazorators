// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class EqualsGreaterThanToken : Token
{
    public EqualsGreaterThanToken() => ((INode)this).Kind = TypeScriptSyntaxKind.EqualsGreaterThanToken;
}
