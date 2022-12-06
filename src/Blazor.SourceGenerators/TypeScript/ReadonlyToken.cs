// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ReadonlyToken : Token
{
    public ReadonlyToken() => ((INode)this).Kind = TypeScriptSyntaxKind.ReadonlyKeyword;
}
