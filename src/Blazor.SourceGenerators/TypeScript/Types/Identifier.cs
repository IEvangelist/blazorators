// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class Identifier : PrimaryExpression, IJsxTagNameExpression, IEntityName, IPropertyName
{
    public Identifier()
    {
        Kind = TypeScriptSyntaxKind.Identifier;
    }

    public string Text { get; set; }
    public TypeScriptSyntaxKind OriginalKeywordKind { get; set; }
    public GeneratedIdentifierKind AutoGenerateKind { get; set; }
    public int AutoGenerateId { get; set; }
    public bool IsInJsDocNamespace { get; set; }
}