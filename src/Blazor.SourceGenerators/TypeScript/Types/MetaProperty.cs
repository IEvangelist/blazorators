// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class MetaProperty : PrimaryExpression
{
    public MetaProperty()
    {
        Kind = TypeScriptSyntaxKind.MetaProperty;
    }

    public TypeScriptSyntaxKind KeywordToken { get; set; }
    public Identifier Name { get; set; }
}