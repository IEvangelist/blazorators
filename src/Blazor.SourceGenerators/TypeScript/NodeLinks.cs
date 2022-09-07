// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class NodeLinks
{
    internal NodeCheckFlags Flags { get; set; }
    internal TsType ResolvedType { get; set; }
    internal Signature ResolvedSignature { get; set; }
    internal Symbol ResolvedSymbol { get; set; }
    internal IndexInfo ResolvedIndexInfo { get; set; }
    internal bool MaybeTypePredicate { get; set; }
    internal int EnumMemberValue { get; set; }
    internal bool IsVisible { get; set; }
    internal bool HasReportedStatementInAmbientContext { get; set; }
    internal JsxFlags JsxFlags { get; set; }
    internal TsType ResolvedJsxElementAttributesType { get; set; }
    internal bool HasSuperCall { get; set; }
    internal ExpressionStatement SuperCall { get; set; }
    internal TsType[] SwitchTypes { get; set; }
}