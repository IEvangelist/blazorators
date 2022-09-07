// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class TransientIdentifier : Identifier
{
    internal Symbol ResolvedSymbol { get; set; }
}
