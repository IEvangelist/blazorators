// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class IdentifierTypePredicate : TypePredicateBase
{
    public IdentifierTypePredicate()
    {
        Kind = TypePredicateKind.Identifier;
    }

    public string ParameterName { get; set; }
    public int ParameterIndex { get; set; }
}