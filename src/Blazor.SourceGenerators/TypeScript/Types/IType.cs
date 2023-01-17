// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public interface IType
{
    TypeFlags Flags { get; set; }
    int Id { get; set; }
    Symbol Symbol { get; set; }
    IDestructuringPattern Pattern { get; set; }
    Symbol AliasSymbol { get; set; }
    TypeScriptType[] AliasTypeArguments { get; set; }
}