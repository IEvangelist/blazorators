// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class AtToken : Token
{
    public AtToken() => ((INode)this).Kind = TypeScriptSyntaxKind.AtToken;
}
