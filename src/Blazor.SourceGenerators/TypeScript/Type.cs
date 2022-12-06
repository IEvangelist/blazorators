// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TsType : IType
{
    TypeFlags IType.Flags { get; set; }
    int IType.Id { get; set; }
    Symbol IType.Symbol? { get; set; }
    IDestructuringPattern IType.Pattern? { get; set; }
    Symbol IType.AliasSymbol? { get; set; }
    TsType[] IType.AliasTypeArguments? { get; set; }
}