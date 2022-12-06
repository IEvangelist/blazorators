// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class IdentifierTypePredicate : TypePredicateBase
{
    public IdentifierTypePredicate() => ((INode)this).Kind = TypePredicateKind.Identifier;

    public string ParameterName? { get; set; }
    public int ParameterIndex { get; set; }
}