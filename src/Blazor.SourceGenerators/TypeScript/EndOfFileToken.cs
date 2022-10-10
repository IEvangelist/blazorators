// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class EndOfFileToken : Token
{
    public EndOfFileToken() => ((INode)this).Kind = SyntaxKind.EndOfFileToken;
}
