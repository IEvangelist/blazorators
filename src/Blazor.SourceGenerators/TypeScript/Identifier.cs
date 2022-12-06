// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class Identifier : PrimaryExpression, IJsxTagNameExpression, IEntityName, IPropertyName
{
    public Identifier() => ((INode)this).Kind = TypeScriptSyntaxKind.Identifier;

    public string Text? { get; set; }
    public TypeScriptSyntaxKind OriginalKeywordKind { get; set; }
    public GeneratedIdentifierKind AutoGenerateKind { get; set; }
    public int AutoGenerateId { get; set; }
    public bool IsInJsDocNamespace { get; set; }
}
