// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class IdentifierTypePredicate : TypePredicateBase
{
    internal IdentifierTypePredicate() => ((INode)this).Kind = TypePredicateKind.Identifier;

    internal string ParameterName { get; set; } = default!;
    internal int ParameterIndex { get; set; }
}