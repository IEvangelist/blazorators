// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ExternalModuleReference : Node
{
    public ExternalModuleReference()
    {
        Kind = TypeScriptSyntaxKind.ExternalModuleReference;
    }

    public IExpression Expression { get; set; }
}