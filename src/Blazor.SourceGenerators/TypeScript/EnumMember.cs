// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class EnumMember : Declaration
{
    public EnumMember() => ((INode)this).Kind = TypeScriptSyntaxKind.EnumMember;

    public IExpression? Initializer { get; set; }
}