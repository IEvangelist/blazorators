// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TsType : IType
{
    TypeFlags IType.Flags { get; set; }
    int IType.Id { get; set; }
    Symbol IType.Symbol { get; set; } = default!;
    IDestructuringPattern IType.Pattern { get; set; } = default!;
    Symbol IType.AliasSymbol { get; set; } = default!;
    TsType[] IType.AliasTypeArguments { get; set; } = default!;
}