// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class AsteriskToken : Token
{
    public AsteriskToken() => ((INode)this).Kind = TypeScriptSyntaxKind.AsteriskToken;
}
