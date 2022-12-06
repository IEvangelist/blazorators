// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public interface IType
{
    public TypeFlags Flags { get; set; }
    public int Id { get; set; }
    public Symbol Symbol { get; set; }
    public IDestructuringPattern Pattern { get; set; }
    public Symbol AliasSymbol { get; set; }
    public TsType[] AliasTypeArguments { get; set; }
}