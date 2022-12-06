// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class EndOfFileToken : Token
{
    public EndOfFileToken() => ((INode)this).Kind = TypeScriptSyntaxKind.EndOfFileToken;
}
