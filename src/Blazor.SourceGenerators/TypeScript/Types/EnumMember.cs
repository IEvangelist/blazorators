// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class EnumMember : Declaration
{
    public EnumMember()
    {
        Kind = TypeScriptSyntaxKind.EnumMember;
    }

    public IExpression Initializer { get; set; }
}