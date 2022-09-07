// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal interface IType
{
    internal TypeFlags Flags { get; set; }
    internal int Id { get; set; }
    internal Symbol Symbol { get; set; }
    internal IDestructuringPattern Pattern { get; set; }
    internal Symbol AliasSymbol { get; set; }
    internal TsType[] AliasTypeArguments { get; set; }
}