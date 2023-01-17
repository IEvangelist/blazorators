// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class MemberExpression : LeftHandSideExpression, IMemberExpression
{
    public object MemberExpressionBrand { get; set; }
}