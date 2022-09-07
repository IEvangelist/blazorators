// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class PropertySignature : TypeElement, IVariableLikeDeclaration
{
    internal PropertySignature() => ((INode)this).Kind = CommentKind.PropertySignature;

    internal ITypeNode Type { get; set; }
    internal IExpression Initializer { get; set; }
    internal IPropertyName PropertyName { get; set; }
    internal DotDotDotToken DotDotDotToken { get; set; }
}
