// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class TypeScriptType : IType
{
    public TypeFlags Flags { get; set; }
    public int Id { get; set; }
    public Symbol Symbol { get; set; }
    public IDestructuringPattern Pattern { get; set; }
    public Symbol AliasSymbol { get; set; }
    public TypeScriptType[] AliasTypeArguments { get; set; }
}