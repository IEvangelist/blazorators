// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class MetaProperty : PrimaryExpression
{
    public MetaProperty() => ((INode)this).Kind = TypeScriptSyntaxKind.MetaProperty;

    public TypeScriptSyntaxKind KeywordToken { get; set; }
    public Identifier Name { get; set; }
}