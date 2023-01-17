// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class NodeLinks
{
    public NodeCheckFlags Flags { get; set; }
    public TypeScriptType ResolvedType { get; set; }
    public Signature ResolvedSignature { get; set; }
    public Symbol ResolvedSymbol { get; set; }
    public IndexInfo ResolvedIndexInfo { get; set; }
    public bool MaybeTypePredicate { get; set; }
    public int EnumMemberValue { get; set; }
    public bool IsVisible { get; set; }
    public bool HasReportedStatementInAmbientContext { get; set; }
    public JsxFlags JsxFlags { get; set; }
    public TypeScriptType ResolvedJsxElementAttributesType { get; set; }
    public bool HasSuperCall { get; set; }
    public ExpressionStatement SuperCall { get; set; }
    public TypeScriptType[] SwitchTypes { get; set; }
}