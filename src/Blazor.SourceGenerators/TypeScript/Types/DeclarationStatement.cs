// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class DeclarationStatement : Node, IDeclarationStatement, IDeclaration, IStatement
{
    public object DeclarationBrand { get; set; }
    public INode Name { get; set; }
    public object StatementBrand { get; set; }
}