// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class ExternalModuleReference : Node
{
    public ExternalModuleReference() => ((INode)this).Kind = TypeScriptSyntaxKind.ExternalModuleReference;

    public IExpression? Expression { get; set; }
}