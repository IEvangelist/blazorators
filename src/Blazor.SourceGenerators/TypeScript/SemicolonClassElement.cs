// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class SemicolonClassElement : ClassElement
{
    public SemicolonClassElement() => ((INode)this).Kind = TypeScriptSyntaxKind.SemicolonClassElement;
}
